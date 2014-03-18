using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Rackspace.Cloud.Server.Common.Commands;
using Rackspace.Cloud.Server.Common.Logging;

namespace Rackspace.Cloud.Server.Common.Restart
{
    public static class RestartManager
    {
        static RestartManager()
        {
            RestartNeeded = false;
            CommandSetsToRun = 1;
            NoCommandsFoundRestartCount = 2;
        }

        public static bool RestartNeeded { get; set; }

        public static int CommandSetsToRun { get; set; }
        public static int NoCommandsFoundRestartCount { get; set; }

        public static void RestartMachine()
        {
            Process.Start(@"shutdown.exe", "/r /t 5 /f /d p:02:04");
            CommandsController.ProcessCommands = false;
        }

        public static void CommandsRunRestartCheck(ILogger logger)
        {
            if (RestartNeeded)
            {
                if (CommandSetsToRun <= 0)
                {
                    logger.Log("Restart needed...");
                    RestartMachine();
                    logger.Log("Restart command sent...");
                }
                CommandSetsToRun -= 1;
            }
        }

        public static void NoCommandsFoundRestartCheck(ILogger logger)
        {
            if (RestartNeeded)
            {
                if (NoCommandsFoundRestartCount <= 0)
                {
                    CommandsRunRestartCheck(logger);
                }
                NoCommandsFoundRestartCount -= 1;
            }
        }
    }
}
