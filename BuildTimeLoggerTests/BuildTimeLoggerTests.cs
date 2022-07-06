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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using BuildTimeLogger.Logger;
using BuildTimeLogger.Models;
using System;

namespace BuildLoggerTests
{
    [TestClass]
    public class BuildTimeLoggerTests
    {

        [TestMethod]
        [DataRow(0, 0, 20, "20.000")]
        [DataRow(0, 1, 20, "80.000")]
        [DataRow(1, 1, 20, "3680.000")]
        public void Test_TimeSpanFormatter(int hours, int minutes, int seconds, string expectedTotalSeconds)
        {
            TimeSpan timespan = new TimeSpan(hours, minutes, seconds);
            string totalSeconds = InfluxDBFormatter.ToSecondsString(timespan);
            Assert.AreEqual(expectedTotalSeconds, totalSeconds);
        }

        [TestMethod]
        [DataRow(2021, 6, 22, 2, 41, 48, DateTimeKind.Local, "1624293708")]
        [DataRow(2021, 6, 22, 2, 41, 48, DateTimeKind.Utc, "1624329708")]
        public void Test_DateTimeFormatter(int year, int month, int day, int hour, int minutes, int seconds, DateTimeKind kind, string expectedUnixTimestamp)
        {
            DateTime dateTime = new DateTime(year, month, day, hour, minutes, seconds, kind);
            string unixTimeStamp = InfluxDBFormatter.ToUnixTimeString(dateTime);
            Assert.AreEqual(expectedUnixTimestamp, unixTimeStamp);
        }

        [TestMethod]
        [DataRow("test_string", "\"test_string\"")]
        public void Test_WrapQuotes(string inputString, string expectedOutputString)
        {
            string outputString = InfluxDBFormatter.WrapQuotes(inputString);
            Assert.AreEqual(expectedOutputString, outputString);
        }

        [TestMethod]
        public void Test_LineProtocol()
        {
            BuildLogModel testModel = new BuildLogModel();

            // Fields
            testModel.BuildStart = new DateTime(2021, 6, 22, 2, 41, 48, DateTimeKind.Local);
            testModel.BuildFinish = new DateTime(2021, 6, 22, 2, 41, 49, DateTimeKind.Local);
            testModel.BuildDuration = testModel.BuildFinish - testModel.BuildStart;

            // Tags
            testModel.User = "Test User";
            testModel.VSVersion = "2019";
            testModel.ExtensionVersion = "vTest";
            testModel.ProjectName = "This is A Test Project";
            testModel.SolutionName = "Test Solution";
            testModel.MachineName = "Test Machine";
            testModel.BuildType = "Test";
            testModel.BuildEventType = "Build";
            testModel.CPUModel = "Awesome CPU";
            testModel.BuildResult = true;
            testModel.CPUCoreCount = 64;
            testModel.CPUThreadCount = 8;
            testModel.RAMSize = 13994848983;
            
            // Construct expected formatting
            string output_template =
                // Measurement
                "build_event," +
                // Tags
                "user={0},vs_version={1},extension_version={2},project_name={3}," +
                "solution_name={4},machine_name={5},build_type={6},build_event_type={7},cpu_model={8}," +
                "build_success={9},cpu_core_count={10},cpu_thread_count={11},ram_size={12} " +
                // Fields
                "build_start={13},build_finish={14},build_duration={15} " +
                // Timestamp
                "{16}"; 

            string expectedOuptut = String.Format(output_template,
                // Tags
                "\"Test\\ User\"", "\"2019\"", "\"vTest\"", "\"This\\ is\\ A\\ Test\\ Project\"", 
                "\"Test\\ Solution\"", "\"Test\\ Machine\"", "\"Test\"", "\"Build\"", "\"Awesome\\ CPU\"", 
                "true", "64", "8", "13994848983",
                // Fields
                "1624293708", "1624293709", "1.000",
                //Timestamp
                "1624293709"
             );

            string actualOutput = InfluxDBFormatter.ToLineProtocol(testModel);

            Assert.AreEqual(expectedOuptut, actualOutput);
        }
    }

}
