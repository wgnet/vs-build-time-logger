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

using BuildTimeLogger.Models;
using BuildTimeLogger.Settings;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace BuildTimeLogger.Logger
{
    public static class InfluxDBFormatter
    {
        public static readonly HashSet<Type> DecimalTypes = new HashSet<Type>
        {
            typeof(float),
            typeof(double)
        };

        public static string ToSecondsString(TimeSpan timeSpan)
        {
            double duration = timeSpan.TotalSeconds;
            return duration.ToString("F3");
        }

        public static string ToUnixTimeString(DateTime date)
        {
            DateTimeOffset dto = new DateTimeOffset(date);
            long unixTime = dto.ToUnixTimeSeconds();
            return unixTime.ToString("F0");
        }

        public static string WrapQuotes(string value)
        {
            return $"\"{value}\"";
        }

        public static string FormatBool(bool value)
        {
            return value ? "true" : "false";
        }

        public static string ToLineProtocol(BuildLogModel buildData)
        {
            string timestamp = ToUnixTimeString(buildData.BuildFinish);

            string fieldString =
                $"build_start={ToUnixTimeString(buildData.BuildStart)}," +
                $"build_finish={timestamp}," +
                $"build_duration={ToSecondsString(buildData.BuildDuration)}";

            fieldString = fieldString.Replace(" ", "\\ ");

            // NOTE: BuildID is omitted from the InfluxDB log because tags that are highly variable create high series
            // cardinality and drastically increases memory usage. Additionally, fields can't be used in GroupBy queries, defeating 
            // the point of this value if used as a field instead of a tag
            // see https://docs.influxdata.com/influxdb/v1.8/concepts/schema_and_data_layout/#avoid-too-many-series for info
            string tagString = 
                $"user={WrapQuotes(buildData.User)}," +
                $"vs_version={WrapQuotes(buildData.VSVersion)}," +
                $"extension_version={WrapQuotes(buildData.ExtensionVersion)}," +
                $"project_name={WrapQuotes(buildData.ProjectName)}," +
                $"solution_name={WrapQuotes(buildData.SolutionName)}," +
                $"machine_name={WrapQuotes(buildData.MachineName)}," +
                $"build_type={WrapQuotes(buildData.BuildType)}," +
                $"build_event_type={WrapQuotes(buildData.BuildEventType)}," +
                $"cpu_model={WrapQuotes(buildData.CPUModel)}," +
                $"build_success={FormatBool(buildData.BuildResult)}," +
                $"cpu_core_count={buildData.CPUCoreCount}," +
                $"cpu_thread_count={buildData.CPUThreadCount}," +
                $"ram_size={buildData.RAMSize}";

            tagString = tagString.Replace(" ", "\\ ");

            return $"build_event,{tagString} {fieldString} {timestamp}";
        }
    }

    /// <summary>
    /// Class that handles writing logs that fail to be sent to a file.
    /// 
    /// Limits cache to be only the last 1000 entries.
    /// </summary>
    public class InfluxDBCache
    {
        const int CACHE_LIMIT = 1000;
        const string CACHE_FOLDER = ".vs-build-logger";
        const string CACHE_FILE = "influxdb-build-cache.txt";
        string homeFolder;
        string cachePath;
        string cacheFilePath;

        public InfluxDBCache()
        {
            homeFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            cachePath = Path.Combine(homeFolder, CACHE_FOLDER);
            cacheFilePath = Path.Combine(cachePath, CACHE_FILE);

            CheckCacheFile();
        }

        /// <summary>
        /// Returns a boolean indicating if the cache has data
        /// </summary>
        /// <returns></returns>
        public bool HasData()
        {
            return Size() > 0;
        }

        /// <summary>
        /// Returns the number of lines in the file
        /// </summary>
        /// <returns></returns>
        public long Size() {
            CheckCacheFile();

            var fileInfo = new FileInfo(cacheFilePath);
            return fileInfo.Length;
        }

        public long NumLines()
        {
            return File.ReadLines(cacheFilePath).Count();
        }

        /// <summary>
        /// Gets all data in the cache
        /// </summary>
        /// <returns></returns>
        public string GetCache()
        {
            CheckCacheFile();

            return File.ReadAllText(cacheFilePath);
        }

        /// <summary>
        /// Removes all data in the cache
        /// </summary>
        public void ClearCache()
        {
            CheckCacheFile();

            File.WriteAllText(cacheFilePath, string.Empty);
            
        }

        /// <summary>
        /// Adds data to the end of the cache
        /// </summary>
        /// <param name="data"></param>
        public void AppendCache(string data)
        {
            CheckCacheFile();

            // Make sure string is clean and ends with newline
            data.Trim();
            if(!data.EndsWith("\n"))
            {
                data = data + "\n";
            }

            // Cycle cache if it's already full
            if(NumLines() >= CACHE_LIMIT)
            {
                // Cycle cache by the number of newlines in the new data entry
                CycleCache(data.Count(c => c == '\n'));
            }


            using (StreamWriter writer = File.AppendText(cacheFilePath))
            {
                writer.Write(data);
            }
        }
        
        /// <summary>
        /// Removes the first X entries as specified by the cycleNum value
        /// </summary>
        /// <param name="cycleNum"></param>
        private void CycleCache(int cycleNum)
        {
            CheckCacheFile(); 

            string tempFile = Path.GetTempFileName();

            using (var sr = new StreamReader(cacheFilePath))
            using (var sw = new StreamWriter(tempFile))
            {
                string line;
                int rowCount = 0;

                while ((line = sr.ReadLine()) != null)
                {
                    if(rowCount > cycleNum)
                    {
                        sw.WriteLine(line);
                    }
                    rowCount++;
                }
            }

            File.Delete(cacheFilePath);
            File.Move(tempFile, cacheFilePath);
            File.Delete(tempFile);
        }

        /// <summary>
        /// Checks to make sure the file and directory exists, and if they don't, 
        /// creates them
        /// </summary>
        private void CheckCacheFile()
        {
            if (!Directory.Exists(cachePath))
            {
                Directory.CreateDirectory(cachePath);
            }

            if (!File.Exists(cacheFilePath))
            {
                File.Create(cacheFilePath);
            }
        }
    }



    /// <summary>
    /// Singleton that handles communication with InfluxDB for logging build information
    /// </summary>
    public sealed class InfluxDBLogger: IBuildLogger
    {
        // The http client to make our requests
        private static readonly HttpClient client = new HttpClient();

        // Set of good status codes that make us happy
        private static readonly HashSet<HttpStatusCode> VALID_RESPONSES = new HashSet<HttpStatusCode> {
            HttpStatusCode.NoContent,
            HttpStatusCode.OK
        };


        // Cache for when logging to the database fails
        private static readonly InfluxDBCache influxCache = new InfluxDBCache();


        /// <summary>
        /// Private construuctor to confrom to singleton pattern imposed by base class
        /// </summary>
        public InfluxDBLogger()
        {
        }


        /// <summary>
        /// Function for logging a single datapoint
        /// </summary>
        /// <param name="data">A single BuildLogModel to serialize</param>
        /// <returns></returns>
        public override async Task LogBuildDataAsync(BuildLogModel data)
        {
            await LogAsync(InfluxDBFormatter.ToLineProtocol(data));
        }

        /// <summary>
        /// Function for logging multiple datapoints
        /// </summary>
        /// <param name="datas">List of BuildLogModel objects to serialize</param>
        /// <returns></returns>
        public override async Task LogBuildDataAsync(List<BuildLogModel> datas)
        {
            // Convert list of data into a newline seperated single string of line protocol
            List<string> logStrings = datas.Select(build => InfluxDBFormatter.ToLineProtocol(build)).ToList();
            string logString = logStrings.Aggregate((current, next) => current + "\n" + next);

            await LogAsync(logString);
        }

        /// <summary>
        /// Function that can be used to test the connection status of InfluxDB
        /// </summary>
        /// <returns></returns>
        public override async Task CheckConnectionAsync()
        {
            // Grab settings instance
            BuildTimeLoggerSettings settings = await BuildTimeLoggerSettings.GetLiveInstanceAsync();
            string url = ((settings.InfluxDBVersion == InfluxDBVersionsEnum.InfluxDBv1) ? settings.InfluxDB1URL : settings.InfluxDB2URL);

            var response = await client.GetAsync($"{url}/ping");

            await CheckResponseAsync(response);
        }

        /// <summary>
        /// Accepts pre-serialised string data, and based on the InfluxDB version configured in the options
        /// passes it on to the corresponding handler
        /// </summary>
        /// <param name="data">Serialised line protocol data to log</param>
        /// <returns></returns>
        private async Task LogAsync(string data)
        {
            // Need to load settings asynchronously since we're not on the UI thread at this point
            BuildTimeLoggerSettings settings = await BuildTimeLoggerSettings.GetLiveInstanceAsync();

            InfluxDBVersionsEnum version = settings.InfluxDBVersion;

            string cacheData = "";

            try
            {
                // If we have cache data, prepend it to string
                if(influxCache.HasData())
                {
                    cacheData = influxCache.GetCache();
                }

                string combinedData = data + "\n" + cacheData;

                // Log data according to influxdb version
                if (version == InfluxDBVersionsEnum.InfluxDBv1)
                {
                    await LogV1Async(combinedData);
                }
                else
                {
                    await LogV2Async(combinedData);
                }

                // If we made it here, logging was successful so clear cache
                influxCache.ClearCache();
            }
            catch (Exception ex)
            {
                // Append these entries to cache
                influxCache.AppendCache(data);

                // Bubble up exception
                throw ex;
            }

        }

        /// <summary>
        /// Creates the write request for InfluxDB 1.8. Uses configured username and password to authenticate.
        /// </summary>
        /// <param name="data">Serialised line protocol data to log</param>
        /// <returns></returns>
        private async Task LogV1Async(string data)
        {
            // Grab settings instance
            BuildTimeLoggerSettings settings = await BuildTimeLoggerSettings.GetLiveInstanceAsync();

            // Extract most recent values for InfluxDB 1 Settings
            string url = settings.InfluxDB1URL;
            string username = settings.InfluxDB1Username;
            string password = settings.InfluxDB1Password;
            string bucket = settings.InfluxDB1Database;

            // Check if we have valid data
            if (String.IsNullOrEmpty(url) || String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password)|| String.IsNullOrEmpty(bucket))
            {
                throw new InvalidOperationException("Not all InfluxDB V1 settings set.");
            }

            // Create the http information needed to hit the v1 write api endpoint
            string writeUrl = $"{url}/write?db={bucket}&u={username}&p={password}&precision=s";
            string content = data;
            
            // Send the request
            await WriteLogAsync(writeUrl, content);
        }

        /// <summary>
        /// Creates the write request for InfluxDB 2.0+. Uses configured token to 
        /// set a token authorisation header.
        /// </summary>
        /// <param name="data">Serialised line protocol data to log</param>
        /// <returns></returns>
        private async Task LogV2Async(string data)
        {
            // Grab settings instance
            BuildTimeLoggerSettings settings = await BuildTimeLoggerSettings.GetLiveInstanceAsync();

            // Extract most recent values for InfluxDB 2 Settings
            string url = settings.InfluxDB2URL;
            string token = settings.InfluxDB2Token;
            string org = settings.InfluxDB2Org;
            string bucket = settings.InfluxDB2Bucket;

            // Check if we have valid data
            if (String.IsNullOrEmpty(url) || String.IsNullOrEmpty(token) || String.IsNullOrEmpty(org) || String.IsNullOrEmpty(bucket))
            {
                throw new InvalidOperationException("Not all InfluxDB V2 settings set.");
            }

            // Create the http information needed to hit the v2 write api endpoint
            string writeUrl = $"{url}/api/v2/write?bucket={bucket}&org={org}&precision=s";
            string content = data;
            string authorization = $"Token {token}";

            // Send the request
            await WriteLogAsync(writeUrl, content, authorization);

        }

        /// <summary>
        /// Function that performs the actual query
        /// </summary>
        /// <param name="writeUrl"></param>
        /// <param name="content"></param>
        /// <param name="authorization"></param>
        /// <returns></returns>
        private async Task WriteLogAsync(string writeUrl, string content, string authorization = null) 
        {
            using (var request = new HttpRequestMessage(new HttpMethod("POST"), writeUrl))
            {
                // Add auth token if we've been given one
                if (authorization != null)
                {
                    request.Headers.Add("Authorization", authorization);
                }

                // Set content and media type
                request.Content = new StringContent(content);
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain; charset=utf-8");

                // Make request!
                var response = await client.SendAsync(request);

                await CheckResponseAsync(response);
            }
        }

        /// <summary>
        /// Helper function to determine if a http response is one we are happy with
        /// </summary>
        /// <param name="response">The http response recieved</param>
        /// <returns></returns>
        private async Task CheckResponseAsync(HttpResponseMessage response)
        {
            // If we don't get a good response, throw an exception so build log users can handle appropriately
            if (!VALID_RESPONSES.Contains(response.StatusCode))
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"HTTP Error - Status Code: {response.StatusCode} Content: {responseContent}");
            }
        } 
    }




}
