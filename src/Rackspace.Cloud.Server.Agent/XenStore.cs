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

using System.Collections.Generic;
using System.Linq;
using Rackspace.Cloud.Server.Agent.Configuration;
using Rackspace.Cloud.Server.Agent.Interfaces;
using Rackspace.Cloud.Server.Agent.Utilities;

namespace Rackspace.Cloud.Server.Agent
{
    public class XenStore : IXenStore
    {
        private readonly IExecutableProcess _executableProcess;

        protected virtual string GetXenClientPath()
        {
            return Constants.XenClientPath;
        }

        public XenStore(IExecutableProcess executableProcess)
        {
            _executableProcess = executableProcess;
        }

        public IEnumerable<string> Read(string key)
        {
            return _executableProcess.Run(GetXenClientPath(), "dir " + key).Output;
        }

        public IList<Command> GetCommands()
        {
            var messageKeysAsUuids = Read(Constants.WritableDataHostBase).ValidateAndClean();
            IList<Command> commands = new List<Command>();
                
            foreach (var messageKey in messageKeysAsUuids)
            {
                var result = ReadKey(messageKey);
                if (result.Contains("The system cannot find the file specified.")) continue;
                var command = new Json<Command>().Deserialize(result);
                command.key = messageKey;
                commands.Add(command);
            }
            return commands;
        }

        public string ReadKey(string key)
        {
            var result = _executableProcess.Run(GetXenClientPath(), "read " + Constants.Combine(Constants.WritableDataHostBase, key));
            return result.Output.First();
        }

        public string ReadVmData(string key)
        {
            var result = _executableProcess.Run(GetXenClientPath(), "read " + Constants.Combine(Constants.ReadOnlyDataConfigBase, key));
            return result.Output.First();
        }

        public string ReadVmDataKey(string key)
        {
            var result = _executableProcess.Run(GetXenClientPath(), "read " + Constants.Combine(Constants.ReadOnlyDataConfigBase, Constants.NetworkingBase, key));
            return result.Output.First();
        }

        public string ReadVmProviderDataKey(string key)
        {
            var result = _executableProcess.Run(GetXenClientPath(), "read " + Constants.Combine(Constants.ReadOnlyDataConfigBase, Constants.ProviderDataBase, key));
            if (result.ExitCode == "0" && result.Output != null && result.Output.Any())
                return result.Output.First();
            return string.Empty;
        }

        public void Write(string key, string value)
        {
            _executableProcess.Run(GetXenClientPath(), "write " + Constants.Combine(Constants.WritableDataGuestBase, key) + " " + value.EscapeQuotesForXenClientWrite());
        }

        public void Remove(string key)
        {
            _executableProcess.Run(GetXenClientPath(), "remove " + Constants.Combine(Constants.WritableDataHostBase, key));
        }
    }
}