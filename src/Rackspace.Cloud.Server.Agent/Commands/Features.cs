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

namespace Rackspace.Cloud.Server.Agent.Commands
{
    [PreAndPostCommand]
    public class Features : IExecutableCommand
    {
        public ExecutableResult Execute(string value)
        {
            var enumValues = "";
            foreach (var val in Enum.GetValues(typeof(Utilities.Commands)))
            {
                if (val.ToString() == "features") continue;
                enumValues += val + ",";
            }
            enumValues = enumValues.Substring(0, enumValues.Length - 1);
            return new ExecutableResult { Output = new List<string> { enumValues } };
        }
    }
}