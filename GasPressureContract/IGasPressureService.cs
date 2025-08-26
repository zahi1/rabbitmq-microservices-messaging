namespace Services
{
    /// <summary>
    /// Descriptor for gas container status.
    /// </summary>
    public class GasContainerStatus
    {
        /// <summary>
        /// Current pressure in the gas container.
        /// </summary>
        public double Pressure { get; set; }

        /// <summary>
        /// Indicates whether the container is destroyed.
        /// </summary>
        public bool IsDestroyed { get; set; }
    }

    /// <summary>
    /// Descriptor for mass adjustment result.
    /// </summary>
    public class MassAdjustmentResult
    {
        /// <summary>
        /// Indicates if the mass adjustment operation was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Reason for failure if the operation was not successful.
        /// </summary>
        public string FailureReason { get; set; }
    }

    /// <summary>
    /// Gas pressure service contract interface.
    /// </summary>
    public interface IGasPressureService
    {
        /// <summary>
        /// Gets the current pressure inside the gas container.
        /// </summary>
        /// <returns>The current pressure value.</returns>
        double GetPressure();

        /// <summary>
        /// Checks if the gas container has been destroyed.
        /// </summary>
        /// <returns>True if the container is destroyed; otherwise, false.</returns>
        bool IsDestroyed();

        /// <summary>
        /// Increases the mass in the gas container by a specified amount.
        /// </summary>
        /// <param name="mass">The amount of mass to add.</param>
        /// <returns>Result of the mass adjustment operation.</returns>
        MassAdjustmentResult IncreaseMass(double mass);

        /// <summary>
        /// Decreases the mass in the gas container by a specified amount.
        /// </summary>
        /// <param name="mass">The amount of mass to remove.</param>
        /// <returns>Result of the mass adjustment operation.</returns>
        MassAdjustmentResult DecreaseMass(double mass);
    }

    /// <summary>
    /// Entry point for the application.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // Entry point for the application; does nothing by design.
            Console.WriteLine("Hello");
        }
    }
}
