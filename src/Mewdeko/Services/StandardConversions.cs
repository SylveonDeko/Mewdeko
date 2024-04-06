namespace Mewdeko.Services
{
    /// <summary>
    /// Provides standard conversion methods.
    /// </summary>
    public static class StandardConversions
    {
        /// <summary>
        /// Converts temperature from Celsius to Fahrenheit.
        /// </summary>
        /// <param name="cel">The temperature in Celsius.</param>
        /// <returns>The equivalent temperature in Fahrenheit.</returns>
        public static double CelsiusToFahrenheit(double cel) => (cel * 1.8f) + 32;
    }
}