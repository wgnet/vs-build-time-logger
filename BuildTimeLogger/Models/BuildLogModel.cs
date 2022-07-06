// Copyright 2021 Wargaming
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;

namespace BuildTimeLogger.Models
{
    public class BuildLogModel
    {

        public string BuildID { get; set; }
        public DateTime BuildStart { get; set; }
        public DateTime BuildFinish { get; set; }
        public TimeSpan BuildDuration { get; set; }
        public string User { get; set; } = "N/A";
        public string VSVersion { get; set; }
        public string ExtensionVersion { get; set; }
        public string ProjectName { get; set; }
        public string SolutionName { get; set; }
        public string MachineName { get; set; }
        public string BuildType { get; set; }
        public string BuildEventType { get; set; }
        public string CPUModel { get; set; }
        public bool BuildResult { get; set; }
        public int CPUCoreCount { get; set; }
        public int CPUThreadCount { get; set; }
        public UInt64 RAMSize { get; set; }

    }
}
