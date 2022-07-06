using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace BuildTimeLogger
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("47a68e4e-7564-42bc-890d-98ff7b8f15cb")]
    public class TestConnectionToolWindow : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestConnectionToolWindow"/> class.
        /// </summary>
        public TestConnectionToolWindow() : base(null)
        {
            this.Caption = "Build Time Logger - Test InfluxDB Connection";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new TestConnectionToolWindowControl();
        }
    }
}
