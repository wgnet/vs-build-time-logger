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


using System.Runtime.InteropServices;

namespace BuildTimeLogger.Settings
{
    class SettingsProvider
    {
        [Guid("526B7744-84D1-429C-AB85-A5EE2DCE448F")]
        public class General: BaseOptionPage<BuildTimeLoggerSettings> { }
    }
}
