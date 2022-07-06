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

using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE;
using BuildTimeLogger.EventMonitor;
using BuildTimeLogger.Settings;
using BuildTimeLogger.Logger;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace BuildTimeLogger
{
    /// <summary>
    /// This is the base package class for this extension that gets registered with Visual Studio. It has been configured to
    /// load asynchronously when Visual Studio starts up after loading a project using the 'ProvideAutoLoad' attribute. It 
    /// also registers an Options page using the 'ProvideOptionPage' attribute, and makes these options exportable/importable
    /// using the 'ProvideProfile' attribute.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(BuildTimeLoggerPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(UIContextGuids.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideOptionPage(typeof(SettingsProvider.General), BuildTimeLoggerConsts.SettingsCategory, BuildTimeLoggerConsts.SettingsPageName, 0, 0, true)]
    [ProvideProfile(typeof(SettingsProvider.General), BuildTimeLoggerConsts.SettingsCategory, BuildTimeLoggerConsts.SettingsPageName, 1000, 1001, isToolsOptionPage: true , DescriptionResourceID = 1002)]
    [ProvideToolWindow(typeof(TestConnectionToolWindow))]
    public sealed class BuildTimeLoggerPackage : AsyncPackage
    {
        // BuildTimeLoggerPackage GUID string.
        public const string PackageGuidString = "9a35dbc8-ada9-46fb-b3fd-c224bd07427b";

        #region Package Members

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Get the version of the pacakge we are
            string pkgVersion = GetVersion();

            // Request the DTE Service to access a bunch of Visual Studio info and events
            DTE dte = await GetServiceAsync(typeof(DTE)) as DTE;

            // Request the Ouput Window service so we can create our own output windows
            IVsOutputWindow outWindow = await GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;

            // Get the Visual Studio Solution Build Manager service
            IVsSolutionBuildManager2 buildManager = await GetServiceAsync(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager2;

            // Bail out if we don't get valid services
            if (dte == null || outWindow == null || buildManager == null ) return;

            // Create a build logger output pane with it's own GUID so we can log output to the user
            Guid buildLoggerGuid = new Guid("88770DAC-57B9-4304-8D00-48A7EAA2F07E");
            outWindow.CreatePane(ref buildLoggerGuid, "Build Logger Extension", Convert.ToInt32(true), Convert.ToInt32(false));
            outWindow.GetPane(buildLoggerGuid, out IVsOutputWindowPane buildLoggerOutputPane);


            // Get our InfluxDB logger class
            InfluxDBLogger influxDBLogger = new InfluxDBLogger();

            // Give it to our singleton logger provider so ui elements can request it
            BuildLoggerProvider provider = BuildLoggerProvider.Instance;
            provider.RegisterLogger(influxDBLogger);

            // Create our build event monitor, that implements a call back interface for build events, and pass it our logger
            BuildEventMonitor buildEventMonitor = new BuildEventMonitor(dte, influxDBLogger, buildLoggerOutputPane, pkgVersion);

            // Register our build event monitor with the build manager
            buildManager.AdviseUpdateSolutionEvents(buildEventMonitor, out _);

            // Initialise the tool window for testing the db connection
            await TestConnectionToolWindowCommand.InitializeAsync(this);

            // Log in our output pain that we successfully initialised!
            buildLoggerOutputPane.OutputString("========== BUILD LOGGER EXTENSION ==========\n");
            buildLoggerOutputPane.OutputString($"Loaded extension version {pkgVersion}\n");
            buildLoggerOutputPane.OutputString($"Detected VS version: {dte.Version}\n");
        }

        private string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyUri = new UriBuilder(assembly.CodeBase);
            var assemblyPath = Uri.UnescapeDataString(assemblyUri.Path);
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            var manifestPath = Path.Combine(assemblyDirectory, "extension.vsixmanifest");

            var doc = new XmlDocument();
            doc.Load(manifestPath);

            if (doc.DocumentElement == null || doc.DocumentElement.Name != "PackageManifest") return null;

            var metaData = doc.DocumentElement.ChildNodes.Cast<XmlElement>().First(x => x.Name == "Metadata");
            var identity = metaData.ChildNodes.Cast<XmlElement>().First(x => x.Name == "Identity");

            return identity.GetAttribute("Version");
        }

        #endregion
    }
}
