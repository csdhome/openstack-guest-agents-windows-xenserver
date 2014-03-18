using System;
using System.Collections.Generic;
using System.Text;

namespace Rackspace.Cloud.Server.Common.Commands
{
    public static class CommandsController
    {
        static CommandsController()
        {
            ProcessCommands = true;
        }

        public static bool ProcessCommands { get; set; }
    }
}
