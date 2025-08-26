namespace Servers
{
    using NLog;
    using Services;
    using System;
    using System.Threading;

    /// <summary>
    /// Holds the state of the gas container, including properties like temperature, mass, and pressure limits.
    /// </summary>
    public class GasContainerState
    {
        /// <summary>
        /// Lock object to ensure thread-safe access to the state.
        /// </summary>
        public readonly object AccessLock = new object();

        /// <summary>
        /// Temperature of the gas in Kelvin, initialized to room temperature.
        /// </summary>
        public double Temperature { get; set; } = 293;

        /// <summary>
        /// Mass of the gas.
        /// </summary>
        public double Mass { get; set; } = 10;

        /// <summary>
        /// Indicates whether the container has been destroyed.
        /// </summary>
        public bool IsDestroyed { get; set; } = false;

        /// <summary>
        /// Pressure limit at which input components stop adding mass.
        /// </summary>
        public double PressureLimit { get; set; } = 100;

        /// <summary>
        /// Upper pressure limit at which output components may start removing mass.
        /// </summary>
        public double UpperPressureLimit { get; set; } = 150;

        /// <summary>
        /// Pressure limit above which the container explodes.
        /// </summary>
        public double ExplosionLimit { get; set; } = 200;

        /// <summary>
        /// Pressure limit below which the container implodes.
        /// </summary>
        public double ImplosionLimit { get; set; } = 10;

        /// <summary>
        /// Calculates the current pressure of the gas using an ideal gas approximation.
        /// </summary>
        public double Pressure => (Mass * Temperature) / 22.4;

        /// <summary>
        /// Resets the container to its initial state after destruction.
        /// </summary>
        public void Reset()
        {
            Mass = 10;
            Temperature = 293;
            IsDestroyed = false;
        }
    }

    /// <summary>
    /// Logic for managing the gas container, including mass adjustments and pressure checks.
    /// </summary>
    class GasPressureLogic : IGasPressureService
    {
        /// <summary>
        /// Logger for this class.
        /// </summary>
        private Logger mLog = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Background task thread.
        /// </summary>
        private Thread mBgTaskThread;

        /// <summary>
        /// The state of the gas container.
        /// </summary>
        private GasContainerState mState = new GasContainerState();

        /// <summary>
        /// Initializes a new instance of the <see cref="GasPressureLogic"/> class and starts the background task.
        /// </summary>
        public GasPressureLogic()
        {
            mBgTaskThread = new Thread(BackgroundTask);
            mBgTaskThread.Start();
        }

        /// <summary>
        /// Background task to periodically adjust temperature and check pressure.
        /// </summary>
        public void BackgroundTask()
        {
            var rnd = new Random();

            while (true)
            {
                Thread.Sleep(2000); // Adjust every 2 seconds

                lock (mState.AccessLock)
                {
                    if (!mState.IsDestroyed)
                    {
                        // Randomly adjust temperature
                        var tempChange = rnd.Next(-15, 16);
                        mState.Temperature += tempChange;
                        mLog.Info($"Temperature changed by {tempChange}K. New temperature: {mState.Temperature}K");

                        // Check pressure limits to see if the container implodes or explodes
                        CheckPressureLimits();
                    }
                    else
                    {
                        mLog.Info("Container destroyed. Resetting state.");
                        mState.Reset();
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the pressure is within safe limits, destroying the container if not.
        /// </summary>
        private void CheckPressureLimits()
        {
            double currentPressure = mState.Pressure;
            mLog.Info($"Current pressure: {currentPressure}");

            if (currentPressure < mState.ImplosionLimit)
            {
                mState.IsDestroyed = true;
                mLog.Warn("Pressure dropped below implosion limit. Container imploded!");
            }
            else if (currentPressure > mState.ExplosionLimit)
            {
                mState.IsDestroyed = true;
                mLog.Warn("Pressure exceeded explosion limit. Container exploded!");
            }
        }

        /// <summary>
        /// Increases the gas mass if the pressure is below the input limit.
        /// </summary>
        /// <param name="mass">Amount of mass to add.</param>
        /// <returns>A MassAdjustmentResult indicating success or failure.</returns>
        public MassAdjustmentResult IncreaseMass(double mass)
        {
            lock (mState.AccessLock)
            {
                if (!mState.IsDestroyed && mState.Pressure < mState.PressureLimit)
                {
                    mState.Mass += mass;
                    mLog.Info($"Mass increased by {mass} units. New mass: {mState.Mass} units.");
                    return new MassAdjustmentResult { IsSuccess = true };
                }
                else
                {
                    var reason = mState.IsDestroyed ? "Container destroyed." : "Pressure too high to add mass.";
                    mLog.Info($"IncreaseMass failed: {reason}");
                    return new MassAdjustmentResult { IsSuccess = false, FailureReason = reason };
                }
            }
        }

        /// <summary>
        /// Decreases the gas mass if the pressure is above the upper limit.
        /// </summary>
        /// <param name="mass">Amount of mass to remove.</param>
        /// <returns>A MassAdjustmentResult indicating success or failure.</returns>
        public MassAdjustmentResult DecreaseMass(double mass)
        {
            lock (mState.AccessLock)
            {
                if (!mState.IsDestroyed && mState.Pressure > mState.UpperPressureLimit)
                {
                    mState.Mass -= mass;
                    mLog.Info($"Mass decreased by {mass} units. New mass: {mState.Mass} units.");
                    return new MassAdjustmentResult { IsSuccess = true };
                }
                else
                {
                    var reason = mState.IsDestroyed ? "Container destroyed." : "Pressure too low to remove mass.";
                    mLog.Info($"DecreaseMass failed: {reason}");
                    return new MassAdjustmentResult { IsSuccess = false, FailureReason = reason };
                }
            }
        }

        /// <summary>
        /// Gets the current pressure of the gas container.
        /// </summary>
        /// <returns>The calculated pressure of the gas container.</returns>
        public double GetPressure()
        {
            lock (mState.AccessLock)
            {
                return mState.Pressure;
            }
        }

        /// <summary>
        /// Checks whether the container has been destroyed due to implosion or explosion.
        /// </summary>
        /// <returns>True if the container is destroyed, otherwise false.</returns>
        public bool IsDestroyed()
        {
            lock (mState.AccessLock)
            {
                return mState.IsDestroyed;
            }
        }
    }
}
