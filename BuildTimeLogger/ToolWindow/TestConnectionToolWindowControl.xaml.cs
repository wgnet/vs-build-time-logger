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

using BuildTimeLogger.Logger;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace BuildTimeLogger
{
    /// <summary>
    /// Interaction logic for TestConnectionToolWindowControl.
    /// </summary>
    public partial class TestConnectionToolWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestConnectionToolWindowControl"/> class.
        /// </summary>
        public TestConnectionToolWindowControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
        private async void button1_ClickAsync(object sender, RoutedEventArgs e)
        {
            // Switch back to the UI thread so we can write to the output window
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Get the status bar service
            IVsStatusbar statusBar = (IVsStatusbar)Package.GetGlobalService(typeof(SVsStatusbar));

            // Use the standard Visual Studio icon for building.
            object icon = (short)Constants.SBAI_General;

            // Start an item animation to give users feedback that something is happening
            statusBar.Animation(1, ref icon);

            // Disable the button while we wait for a http response
            button1.IsEnabled = false;

            // Window title
            string windowTitle = "Build Time Logger - Test Connection";

            // The logger to use to check connection status
            IBuildLogger logger = BuildLoggerProvider.Instance.GetLogger();

            try
            {
                await logger.CheckConnectionAsync();
                MessageBox.Show(
                    "InfluxDB Connection Works!",
                    windowTitle,
                    MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                string errorMessage = $"ERROR: Connection Failed \n Error Message: {ex.Message}";
                MessageBox.Show(errorMessage, windowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Stop the animation
                statusBar.Animation(0, ref icon);

                // Re-enable the button
                button1.IsEnabled = true;
            }

        }
    }
}