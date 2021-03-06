// Copyright 2011 OpenStack LLC.
// All Rights Reserved.
//
//    Licensed under the Apache License, Version 2.0 (the "License"); you may
//    not use this file except in compliance with the License. You may obtain
//    a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
//    WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
//    License for the specific language governing permissions and limitations
//    under the License.

using System;
using System.Collections.Generic;
using Rackspace.Cloud.Server.Agent.Configuration;
using Rackspace.Cloud.Server.Agent.Interfaces;
using Rackspace.Cloud.Server.Agent.Utilities;
using Rackspace.Cloud.Server.Agent.WMI;
using Rackspace.Cloud.Server.Common.Logging;
using System.Linq;

namespace Rackspace.Cloud.Server.Agent.Actions
{

    public interface ISetNetworkInterface
    {
        void Execute(List<NetworkInterface> networkInterfaces);
    }

    public class SetNetworkInterface : ISetNetworkInterface
    {
        public const int NO_OF_RETRIES_FOR_SETTING_INTERFACE_NAME = 10;
        private readonly IExecutableProcessQueue _executableProcessQueue;
        private readonly IWmiMacNetworkNameGetter _wmiMacNetworkNameGetter;
        private readonly ILogger _logger;
        private readonly IIPFinder _ipFinder;

        public SetNetworkInterface(IExecutableProcessQueue executableProcessQueue, IWmiMacNetworkNameGetter wmiMacNetworkNameGetter, ILogger logger, IIPFinder ipFinder)
        {
            _executableProcessQueue = executableProcessQueue;
            _wmiMacNetworkNameGetter = wmiMacNetworkNameGetter;
            _logger = logger;
            _ipFinder = ipFinder;
        }

        public void Execute(List<NetworkInterface> networkInterfaces)
        {
            var nameAndMacs = _wmiMacNetworkNameGetter.Get();
            if (WereInterfacesEnabled(nameAndMacs)) nameAndMacs = _wmiMacNetworkNameGetter.Get();
            LogLocalInterfaces(nameAndMacs);

            VerifyAllNetworkInterfacesFoundOnMachine(nameAndMacs, networkInterfaces);

            //Cleaning up Network configurations on all nic's prior to trying to set network information
            //we found cases where the network information may be swapped between nics this corrects that scenario
            foreach (var networkName in ReverseSortWithKey(nameAndMacs))
            {
                var matchedNetworkInterface = networkInterfaces.Find(x => nameAndMacs[networkName].Equals(x.mac.ToUpper()));
                if (matchedNetworkInterface != null)
                {
                    CleanseInterfaceForSetup(networkName);
                }
            }

            foreach (var networkName in ReverseSortWithKey(nameAndMacs))
            {
                var matchedNetworkInterface = networkInterfaces.Find(x => nameAndMacs[networkName].Equals(x.mac.ToUpper()));
                if (matchedNetworkInterface != null)
                    SetNetworkInterfaceValues(matchedNetworkInterface, networkName);
            }
        }

        private void VerifyAllNetworkInterfacesFoundOnMachine(IDictionary<string, string> nameAndMacs, List<NetworkInterface> networkInterfaces)
        {
            var networkInterfaceNotFoundOnMachine = networkInterfaces.Find(x => nameAndMacs.FindKey(x.mac.ToUpper()) == null);
            if (networkInterfaceNotFoundOnMachine != null)
                throw new ApplicationException(String.Format("Interface with MAC Addres {0} not found on machine", networkInterfaceNotFoundOnMachine.mac));
        }

        private string[] ReverseSortWithKey(IDictionary<string, string> keyValuePair)
        {
            var allKeys = keyValuePair.Keys.ToArray();
            Array.Sort(allKeys); Array.Reverse(allKeys);
            return allKeys;
        }

        private void SetNetworkInterfaceValues(NetworkInterface networkInterface, string interfaceName)
        {
            SetupIpv4Interface(interfaceName, networkInterface);
            SetupIpv6Interface(interfaceName, networkInterface);

            if (networkInterface.dns != null && networkInterface.dns.Length > 0)
            {
                CleanseDnsForSetup(interfaceName);
                SetupDns(interfaceName, networkInterface);
            }

            _executableProcessQueue.Go();

            SetInterfaceName(networkInterface, interfaceName, 0);
        }

        private void SetInterfaceName(NetworkInterface networkInterface, string interfaceName, int count)
        {
            if (interfaceName != networkInterface.label)
                _executableProcessQueue.Enqueue("netsh",
                                                String.Format("interface set interface name=\"{0}\" newname=\"{1}\"",
                                                              interfaceName, networkInterface.label + count));
            try
            {
                _executableProcessQueue.Go();
            }
            catch (UnsuccessfulCommandExecutionException e)
            {
                _logger.Log(string.Format("Failed to setinterface name to {0} retrying", networkInterface.label + count));
                if (count < NO_OF_RETRIES_FOR_SETTING_INTERFACE_NAME)
                    SetInterfaceName(networkInterface, interfaceName, ++count);
            }
        }

        private void SetupIpv6Interface(string interfaceName, NetworkInterface networkInterface)
        {
            if (networkInterface.ip6s == null || networkInterface.ip6s.Length == 0) return;
            var ip6tuple = networkInterface.ip6s[0];
            if (ip6tuple.enabled != "1") return;
            string command = string.Format("interface ipv6 add address interface=\"{0}\" address={1}/{2}",
                interfaceName, ip6tuple.ip, ip6tuple.netmask);
            _executableProcessQueue.Enqueue("netsh", command);

            command = string.Format("interface ipv6 add route prefix=::/0 interface=\"{0}\" nexthop={1} publish=Yes",
                interfaceName, ip6tuple.gateway);
            _executableProcessQueue.Enqueue("netsh", command, new [] { "0" , "1" });
        }

        private void LogLocalInterfaces(IDictionary<string, string> nameAndMacs)
        {
            _logger.Log("Network Interfaces found locally:");
            foreach (var networkInterface in nameAndMacs)
            {
                _logger.Log(String.Format("{0} ({1})", networkInterface.Key, networkInterface.Value));
            }
        }

        private void SetupIpv4Interface(string interfaceName, NetworkInterface networkInterface)
        {
            var primaryIpHasBeenAssigned = false;
            for (var i = 0; i != networkInterface.ips.Length; i++)
            {
                if (networkInterface.ips[i].enabled != "1") continue;
                if (!string.IsNullOrEmpty(networkInterface.gateway) && !primaryIpHasBeenAssigned)
                {
                    _executableProcessQueue.Enqueue("netsh",
                                                    String.Format(
                                                        "interface ip add address name=\"{0}\" addr={1} mask={2} gateway={3} gwmetric=2",
                                                        interfaceName, networkInterface.ips[i].ip, networkInterface.ips[i].netmask, networkInterface.gateway));
                    primaryIpHasBeenAssigned = true;
                    continue;
                }

                _executableProcessQueue.Enqueue("netsh", String.Format("interface ip add address name=\"{0}\" addr={1} mask={2}",
                                                                       interfaceName, networkInterface.ips[i].ip, networkInterface.ips[i].netmask));
            }
        }

        private void SetupDns(string interfaceName, NetworkInterface networkInterface)
        {
            // Remove Duplicate DNS entries if any.
            var distinctDNSEntries = networkInterface.dns.Distinct().ToArray();
            var originalDNSEntries = networkInterface.dns;

            if (originalDNSEntries.Length != distinctDNSEntries.Length)
            {
                networkInterface.dns = distinctDNSEntries;

                _logger.Log(string.Format("Removed duplicate DNS entries. Before {0}. After {1}", string.Join(", ", originalDNSEntries.ToArray()), string.Join(", ", distinctDNSEntries.ToArray())));
            }

            for (var i = 0; i != networkInterface.dns.Length; i++)
            {
                _executableProcessQueue.Enqueue("netsh", String.Format("interface ip add dns name=\"{0}\" addr={1} index={2}",
                                                                       interfaceName, networkInterface.dns[i], i + 1));
            }
        }

        private void CleanseInterfaceForSetup(string interfaceName)
        {
            // In Windows 2012 by default interfaces are DISABLED. 
            _executableProcessQueue.Enqueue("netsh",
                                            string.Format("interface set interface name=\"{0}\" admin=ENABLED",
                                                          interfaceName));
            _executableProcessQueue.Enqueue("netsh",
                                            string.Format("interface ip set address name=\"{0}\" source=dhcp",
                                                          interfaceName), new[] { "0", "1" });
            foreach (var ipv6Address in _ipFinder.findIpv6Addresses(interfaceName))
            {
                #region Windows 2012 Hack
                // - Adding an ipv6 interface address and removing it, if not the delete ipv6 address fails with "The system cannot find the file specified."
                var addAddresscommand = string.Format("interface ipv6 add address interface=\"{0}\" address=1::",
                                            interfaceName);
                _executableProcessQueue.Enqueue("netsh", addAddresscommand);

                var deleteAddresscommand = string.Format("interface ipv6 delete address interface=\"{0}\" address=1::",
                                            interfaceName);
                _executableProcessQueue.Enqueue("netsh", deleteAddresscommand);

                #endregion
                // Do the real thing :-)
                //Address returned is of the format 'address%scope'
                string address = ipv6Address.ToString().Split('%')[0].ToUpper();
                var command = string.Format("interface ipv6 delete address interface=\"{0}\" address={1}",
                                            interfaceName, address);
                _executableProcessQueue.Enqueue("netsh", command);
            }
            _executableProcessQueue.Enqueue("netsh",
                                            string.Format("interface ipv6 delete route ::/0 \"{0}\"", interfaceName),
                                            new[] { "0", "1" });
        }

        private void CleanseDnsForSetup(string interfaceName)
        {
            _executableProcessQueue.Enqueue("netsh", string.Format("interface ip set dns name=\"{0}\" source=dhcp", interfaceName), new[] { "0", "1" });
        }

        private bool WereInterfacesEnabled(IEnumerable<KeyValuePair<string, string>> nameAndMacs)
        {
            var wereMacsEnabled = false;
            foreach (var nameAndMac in nameAndMacs)
            {
                if (nameAndMac.Value != string.Empty) continue;
                _executableProcessQueue.Enqueue("netsh", String.Format("interface set interface name=\"{0}\" admin=ENABLED", nameAndMac.Key));
                _executableProcessQueue.Go();
                wereMacsEnabled = true;
            }

            return wereMacsEnabled;
        }
    }
}