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

using System.ComponentModel;

namespace BuildTimeLogger.Settings
{
    class BuildTimeLoggerSettings: BaseOptionModel<BuildTimeLoggerSettings>
    {

        // ****** Common InfluxDB Settings
        private const string InfluxDB = "1 - InfluxDB General";

        [Category(InfluxDB)]
        [DisplayName("Logging Enabled")]
        [Description("Whether to log build data at all")]
        [DefaultValue(true)]
        public bool LoggingEnabled { get; set; } = true;

        // Version to use
        [Category(InfluxDB)]
        [DisplayName("InfluxDB Version")]
        [Description("Which version of InfluxDB to configure for")]
        [DefaultValue(InfluxDBVersionsEnum.InfluxDBv1)]
        [TypeConverter(typeof(EnumConverter))]
        public InfluxDBVersionsEnum InfluxDBVersion { get; set; } = InfluxDBVersionsEnum.InfluxDBv1;

        [Category(InfluxDB)]
        [DisplayName("Log User")]
        [Description("Whether to log user name with build log")]
        [DefaultValue(false)]
        public bool LogUser{ get; set; } = false;

        // ****** InfluxDB 2.0 Settings
        private const string InfluxDB2 = "2 - InfluxDB 2.0 Options";

        // Url
        [Category(InfluxDB2)]
        [DisplayName("1 - InfluxDB URL")]
        [Description("Url of InfluxDB instance to push data to")]
        [DefaultValue("")]
        public string InfluxDB2URL { get; set; } = "";

        // Database
        [Category(InfluxDB2)]
        [DisplayName("2 - InfluxDB Bucket")]
        [Description("Bucket to write to in InfluxDB instance")]
        [DefaultValue("")]
        public string InfluxDB2Bucket { get; set; } = "";

        // Organisation
        [Category(InfluxDB2)]
        [DisplayName("3 - InfluxDB Organisation")]
        [Description("Organisation the bucket belongs to")]
        [DefaultValue("")]
        public string InfluxDB2Org { get; set; } = "";

        // Token
        [Category(InfluxDB2)]
        [DisplayName("4 - InfluxDB Token")]
        [Description("Token for authenticating with InfluxDB")]
        [DefaultValue("")]
        public string InfluxDB2Token { get; set; } = "";


        // ****** InfluxDB 1.8 Settings
        private const string InfluxDB1 = "3 - InfluxDB 1.0 Options";

        // Url
        [Category(InfluxDB1)]
        [DisplayName("1 - InfluxDB URL")]
        [Description("Url of InfluxDB instance to push data to")]
        [DefaultValue("")]
        public string InfluxDB1URL { get; set; } = "";

        // Database
        [Category(InfluxDB1)]
        [DisplayName("2 - InfluxDB Database")]
        [Description("Database to write to in InfluxDB instance")]
        [DefaultValue("")]
        public string InfluxDB1Database { get; set; } = "";

        // Username
        [Category(InfluxDB1)]
        [DisplayName("3 - InfluxDB User Name")]
        [Description("InfluxDB user name for authentication")]
        [DefaultValue("")]
        public string InfluxDB1Username { get; set; } = "";

        // Password
        [Category(InfluxDB1)]
        [DisplayName("4 - InfluxDB Password")]
        [Description("InfluxDB user password for authentication")]
        [DefaultValue("")]
        [PasswordPropertyText(true)]
        public string InfluxDB1Password { get; set; } = "";
    }

    public enum InfluxDBVersionsEnum
    {
        InfluxDBv2,
        InfluxDBv1
    };
}
