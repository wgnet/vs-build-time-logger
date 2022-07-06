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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Management;
using System.IO;
using System.Diagnostics;
using EnvDTE;
using BuildTimeLogger.Logger;
using BuildTimeLogger.Settings;

namespace BuildTimeLogger.EventMonitor
{
    /// <summary>
    /// Class that implements the IVsUpdateSolutionEvents2 interface and listens for solution events.
    /// See https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.ivsupdatesolutionevents2?view=visualstudiosdk-2019 for
    /// information about the functions this interface defines
    /// </summary>
    class BuildEventMonitor : IVsUpdateSolutionEvents2
    {
        // Dictionary that maps a dwAction value (passed in in UpdateProjectCfg_Begin) to
        // a string representation of what that action id means. Used to filter only the events we're interested in
        readonly Dictionary<VSSOLNBUILDUPDATEFLAGS, string> BuildEventDict = new Dictionary<VSSOLNBUILDUPDATEFLAGS, string>
            {
                { VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD, "build"},
                { VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_FORCE_UPDATE | VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD, "rebuild" }
            };

        // Maps the string version number extraction from DTE to the full VS name
        readonly Dictionary<string, string> VSVersionDict = new Dictionary<string, string>
        {
            { "16.0", "VS2019"},
            { "15.0", "VS2017"}
        };

        // Local storage of data that should only be queried once
        private string machineName;
        private string cpuModel;
        private int coreCount;
        private int threadCount;
        private UInt64 ramSize;
        private string vsVersion;

        // Flag for generating a new unique build guid to group projects under one 'build pass'
        private Guid currentGuid;

        // Map of actively building projects
        private readonly Dictionary<string, BuildLogModel> activeBuilds;

        // List of projects that have finished building for this build pass
        private readonly List<BuildLogModel> finishedBuilds;

        // Reference to the DTE service for querying additional solution/project information
        private readonly DTE dteService;

        // Reference to the output pane for us to log information
        private readonly IVsOutputWindowPane buildPane;

        // Logger to use when build completes
        private readonly IBuildLogger buildLogger;

        // Version of the package we blelong to
        private readonly string pkgVersion;

        public BuildEventMonitor(DTE dte, IBuildLogger logger, IVsOutputWindowPane buildPane, string pkgVersion)
        {
            // Store reference to build logger
            this.buildLogger = logger;

            // Store reference to dte service
            this.dteService = dte;

            // Store reference to output window
            this.buildPane = buildPane;

            // Create dictionary to hold actively compiling projects
            this.activeBuilds = new Dictionary<string, BuildLogModel>();

            // Create list to hold all finished builds for a solution build pass
            this.finishedBuilds = new List<BuildLogModel>();

            // Store package version
            this.pkgVersion = pkgVersion;

            // Get one time query info
            GatherMachineInfo();
        }


        public int UpdateSolution_Begin(ref int pfCancelUpdate)
        {
            // Generate a new guid for this build pass
            currentGuid = Guid.NewGuid();

            return VSConstants.S_OK;
        }

        public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
        {

            // Grab settings instance
            BuildTimeLoggerSettings settings = BuildTimeLoggerSettings.Instance;
            // If we had valid builds for this solution event, log their information and attempt to push them
            if (finishedBuilds.Count > 0)
            {
                // Log build information
                WriteOutputLine($"Build info for last build run:");
                foreach (var build in finishedBuilds)
                {
                    WriteOutputLine($"{build.ProjectName} finished {(build.BuildResult ? "successfully" : "unsuccessfully")} in {build.BuildDuration}.");
                }

                if(settings.LoggingEnabled)
                {
                    // We don't need this to finish before we return from this function, as we're happy for the code to continue
                    // on another thread at a later time so we disable warnings to do with not awaiting an async function
#pragma warning disable CS4014 
#pragma warning disable VSTHRD110
                    // Once all builds are done, log all the finished builds
                    this.LogFinishedBuildsAsync(settings.LoggingEnabled);
#pragma warning restore VSTHRD110
#pragma warning restore CS4014
                } else
                {
                    WriteOutputLine("Logging disabled - no log information will be sent");
                    ClearBuilds();
                }
            }                
            else
            {
                WriteOutputLine($"No valid solution events detected. No build information logged.");
            }
 
            return VSConstants.S_OK;
        }

        public int UpdateSolution_StartUpdate(ref int pfCancelUpdate)
        {
            // Write information about build
            WriteOutputLine("-------------------------------------------------------------");
            WriteOutputLine($"Detected new solution event. Assigning id {currentGuid}");

            return VSConstants.S_OK;
        }

        public int UpdateSolution_Cancel()
        {
            // Clear all active builds as they've been cancelled
            ClearBuilds();

            return VSConstants.S_OK;
        }

        public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int UpdateProjectCfg_Begin(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, ref int pfCancel)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Return if we don't have valid project info or the action is not one we're listening for
            if(pHierProj == null || pCfgProj == null || !BuildEventDict.ContainsKey((VSSOLNBUILDUPDATEFLAGS)dwAction))
            {
                // Not technically an error, just an event we want to ignore
                return VSConstants.S_OK;
            }

            // Grab settings instance
            BuildTimeLoggerSettings settings = BuildTimeLoggerSettings.Instance;

            // Extract variable project information
            pHierProj.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_Name, out object projName);
            pCfgProj.get_DisplayName(out string configName);
            string projectName = projName.ToString();
            string buildEventType = BuildEventDict[(VSSOLNBUILDUPDATEFLAGS)dwAction];
            string solutionName = Path.GetFileName(dteService.Solution.FileName);

            BuildLogModel buildLog = new BuildLogModel
            {
                BuildID = currentGuid.ToString(),
                BuildStart = DateTime.Now,
                BuildType = configName,
                ProjectName = projectName,
                SolutionName = solutionName,
                MachineName = machineName,
                CPUModel = cpuModel,
                CPUCoreCount = coreCount,
                CPUThreadCount = threadCount,
                RAMSize = ramSize,
                BuildEventType = buildEventType,
                VSVersion = vsVersion,
                ExtensionVersion = pkgVersion
            };

            // Add specific user information if the settings have said it to be so
            if (settings.LogUser)
            {
                buildLog.User = Environment.UserName;
            }

            // Register as an active building project
            activeBuilds[$"{projectName}-{configName}"] = buildLog;

            return VSConstants.S_OK;
        }

        public int UpdateProjectCfg_Done(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, int fSuccess, int fCancel)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Return if we don't have valid project info or the action is not one we're listening for
            if (pHierProj == null || pCfgProj == null || !BuildEventDict.ContainsKey((VSSOLNBUILDUPDATEFLAGS)dwAction))
            {
                // Not technically an error, just an event we want to ignore
                return VSConstants.S_OK;
            }

            // Get project name + config
            pHierProj.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_Name, out object projectName);
            pCfgProj.get_DisplayName(out string configName);

            // Construct key for lookup into existing active builds
            string projKey = $"{projectName}-{configName}";

            // If we have a previously active build for this project, log it
            if (activeBuilds.ContainsKey(projKey))
            {
                BuildLogModel buildLog = activeBuilds[projKey];

                buildLog.BuildFinish = DateTime.Now;
                buildLog.BuildDuration = buildLog.BuildFinish - buildLog.BuildStart;
                buildLog.BuildResult = (fSuccess != 0);

                finishedBuilds.Add(buildLog);
            }

            return VSConstants.S_OK;
        }

        /*******************************
         * PRIVATE METHODS
         ******************************/

        /// <summary>
        /// Function that queries static information about the environment, including machine name, cpu model, core count,
        /// thread count, ram size, and the name of the currently loaded solution.
        /// </summary>
        private void GatherMachineInfo()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.vsVersion = "Unkown";

            // Get the version of Visual Studio we're running in
            if (VSVersionDict.ContainsKey(this.dteService.Version))
            {
                this.vsVersion = VSVersionDict[this.dteService.Version];
            }

            // Get the computer name
            machineName = Environment.MachineName;

            ManagementObjectSearcher cpuQuery = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
            foreach (ManagementObject data in cpuQuery.Get())
            {
                cpuModel = data["Name"].ToString();
                coreCount += int.Parse(data["NumberOfCores"].ToString());
                threadCount += int.Parse(data["NumberOfLogicalProcessors"].ToString());
            }

            ManagementObjectSearcher memQuery = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            foreach (ManagementObject data in memQuery.Get())
            {
                ramSize += Convert.ToUInt64(data.Properties["Capacity"].Value);
            }
        }


        /// <summary>
        /// Removes all project build data from the active builds and finish build lists
        /// </summary>
        private void ClearBuilds()
        {
            this.finishedBuilds.Clear();
            this.activeBuilds.Clear();
        }

        /// <summary>
        /// This is the critical function that initiates the log request to InfluxDB. It has the top level
        /// try/catch block to handle any errors that occur in subsequent function calls.
        /// </summary>
        /// <returns></returns>
        private async System.Threading.Tasks.Task LogFinishedBuildsAsync(bool loggingEnabled)
        {
            // Try to log the finished build information
            try
            {                
                // Switch to a new thread to log the build data asynchronously
                await System.Threading.Tasks.Task.Run(async delegate{                     
                    await buildLogger.LogBuildDataAsync(finishedBuilds);
                });

                // Switch back to the UI thread so we can write to the output window
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                WriteOutputLine($"Build log for {currentGuid} successfully writtten.");

            }
            catch (Exception ex)
            {
                // Run the rest of the function on the UI thread so we can write to the outpuut window
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                WriteOutputLine("ERROR: Failed to log build time information.");
                WriteOutputLine($"Error recieved: {ex.Message}");

                // Will write stack trace to output window in debugging instance of Visual Studio 
                // Ignored in release builds (i.e. won't log to window in installed Visual Studio instance)
                Debug.WriteLine(ex.StackTrace);
            }
            finally
            {
                ClearBuilds();
            }
        }

        /// <summary>
        /// Helper function for writing output to the outputPane
        /// </summary>
        /// <param name="message"></param>
        private void WriteOutputLine(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (buildPane != null)
            {
                buildPane.OutputString($"{message}\n");
            }
        }

    }
}
