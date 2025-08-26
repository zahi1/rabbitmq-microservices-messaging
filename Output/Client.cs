using System;
using System.Threading;
using NLog;

namespace Clients
{
    /// <summary>
    /// Client for managing gas pressure in a container by removing mass when pressure exceeds a specified threshold.
    /// </summary>
    class Client
    {
        /// <summary>
        /// Logger for this class.
        /// </summary>
        private Logger mLog = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Configures the logging subsystem using NLog.
        /// </summary>
        private void ConfigureLogging()
        {
            // Set up NLog configuration
            var config = new NLog.Config.LoggingConfiguration();

            // Console target for log messages
            var console = new NLog.Targets.ConsoleTarget("console")
            {
                Layout = @"${date:format=HH\:mm\:ss}|${level}| ${message} ${exception}"
            };
            config.AddTarget(console);
            config.AddRuleForAllLevels(console);

            // Apply the configuration
            LogManager.Configuration = config;
        }

        /// <summary>
        /// Program body where gas pressure management logic is executed.
        /// </summary>
        private void Run()
        {
            // Configure logging
            ConfigureLogging();
            mLog.Info("Starting Output Client for Gas Pressure Management...");

            // Initialize random number generator for random mass decrements
            var rnd = new Random();

            // Run the client operations in a loop to handle potential disconnections
            while (true)
            {
                try
                {
                    // Connect to the gas pressure service using the GasPressureClient (RabbitMQ client)
                    var gasClient = new GasPressureClient();
                    mLog.Info("Connected to Gas Pressure Service.");

                    // Main loop for gas pressure operations
                    while (true)
                    {
                        // Check if the container is destroyed
                        if (gasClient.IsDestroyed())
                        {
                            mLog.Warn("The gas container has been destroyed. Stopping operations.");
                            break; // Exit the loop if the container is destroyed
                        }

                        // Retrieve the current pressure from the gas container
                        double currentPressure = gasClient.GetPressure();
                        mLog.Info($"Current pressure: {currentPressure} units.");

                        // If pressure is above the upper threshold, attempt to remove mass
                        if (currentPressure > 100)
                        {
                            // Generate a random amount of mass to remove (between 1 and 4 units)
                            int massToRemove = rnd.Next(1, 5);

                            // Attempt to decrease the mass in the gas container
                            gasClient.DecreaseMass(massToRemove);
                            mLog.Info($"Removed {massToRemove} units of mass from the container.");
                        }
                        else
                        {
                            mLog.Info("Pressure is below threshold, no mass removed.");
                        }

                        // Sleep before the next operation to avoid excessive polling
                        Thread.Sleep(2000);
                    }
                }
                catch (Exception e)
                {
                    // Log any exceptions and restart the main loop
                    mLog.Warn(e, "Unhandled exception caught. Restarting main loop...");

                    // Prevent excessive retrying by pausing briefly
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
            // Initialize and run the client
            var self = new Client();
            self.Run();
        }
    }
}
