using System;
using System.Diagnostics;
using Rackspace.Cloud.Server.Agent.Configuration;
using Rackspace.Cloud.Server.Agent.Interfaces;
using Rackspace.Cloud.Server.Common.Configuration;
using Rackspace.Cloud.Server.Common.Logging;

namespace Rackspace.Cloud.Server.Agent
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PreAndPostCommandAttribute : Attribute
    {

        private readonly ILogger _logger;

        public PreAndPostCommandAttribute()
        {
        }

        public PreAndPostCommandAttribute(ILogger logger)
        {
            _logger = logger;
        }

        public ExecutableResult Execute(IExecutableCommand commandExecutable, Command command)
        {
            runInSafeBlock(SvcConfiguration.PreHookPath(command.name));
            var result = commandExecutable.Execute(command.value);
            if (result.ExitCode == "0")
            {
                runInSafeBlock(SvcConfiguration.PostHookPath(command.name));
            }
            else
            {
                _logger.Log(string.Format("Bypassing post command hook, received an exit code of {0} from the command {1}", result.ExitCode, command.name));
            }
            return result;
        }

        private void runInSafeBlock(string command)
        {
            try
            {
                if(String.IsNullOrEmpty(command)) return;

                _logger.Log(string.Format("Starting pre/post hook {0}", command));
                using (var exeProcess = Process.Start(command))
                {
                    exeProcess.WaitForExit();
                }
                _logger.Log(string.Format("Done executing pre/post hook {0}", command));
            }
            catch (Exception e)
            {
                _logger.Log("Error running pre/post commit hook, Error is ignored and continuing with command execution");
                _logger.Log("Exception was : " + e.Message + "\nStackTrace Was: " + e.StackTrace);
            }
        }
    }
}