using System;

namespace BuildTimeLogger.Logger
{
    /// <summary>
    /// Singleton class that wraps the current global logger being used. Primarily used for
    /// being able to access the logger in UI elements where injecting a dependency is difficult.
    /// </summary>
    public class BuildLoggerProvider
    {
        // Logger to provide when requested
        private IBuildLogger buildLogger;

        // Lazy singleton initializor/reference
        private static readonly Lazy<BuildLoggerProvider> lazySelf = new Lazy<BuildLoggerProvider>(() => new BuildLoggerProvider());

        // Singleton accessor
        public static BuildLoggerProvider Instance
        {
            get
            {
                return lazySelf.Value;
            }
        }

        private BuildLoggerProvider() { }

        /// <summary>
        /// Function to register the active logger - should be done as early in the program init
        /// sequence as possible
        /// </summary>
        /// <param name="logger"></param>
        public void RegisterLogger(IBuildLogger logger)
        {
            this.buildLogger = logger;

        }

        /// <summary>
        /// Returns the logger instance that this provider wraps
        /// </summary>
        /// <returns></returns>
        public IBuildLogger GetLogger()
        {
            if(buildLogger == null)
            {
                throw new InvalidOperationException("Logger not set in LoggerProvider - cannot get a null logger");
            }

            return buildLogger;
        }
           
    }
}
