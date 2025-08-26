namespace Servers
{
    using NLog;
    using System;
    using System.Threading;

    /// <summary>
    /// Gas Pressure Management Server
    /// </summary>
    class Server
    {
        /// <summary>
        /// Logger for this class.
        /// </summary>
        private Logger log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Configures the logging subsystem using NLog.
        /// </summary>
        private void ConfigureLogging()
        {
            var config = new NLog.Config.LoggingConfiguration();

            var console = new NLog.Targets.ConsoleTarget("console")
            {
                Layout = @"${date:format=HH\:mm\:ss}|${level}| ${message} ${exception}"
            };
            config.AddTarget(console);
            config.AddRuleForAllLevels(console);

            LogManager.Configuration = config;
        }

        /// <summary>
        /// Program body for running the gas pressure management server.
        /// </summary>
        private void Run()
        {
            // Configure logging
            ConfigureLogging();
            log.Info("Starting Gas Pressure Management Server...");

            while (true)
            {
                try
                {
                    // Start the gas pressure service
                    var service = new GasPressureService();
                    log.Info("Gas Pressure Service has started successfully.");

                    // Keep the main thread alive
                    while (true)
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception e)
                {
                    // Log the exception and restart the server
                    log.Error(e, "Unhandled exception caught. Server will now restart.");

                    // Prevent excessive logging
                    Thread.Sleep(2000);
                }
            }
        }

        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        static void Main(string[] args)
        {
            var server = new Server();
            server.Run();
        }
    }
}
