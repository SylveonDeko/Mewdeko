using System.Security.Cryptography;

namespace Mewdeko.Common
{
    /// <summary>
    /// A class that provides cryptographically secure random number generation.
    /// Inherits from the <see cref="System.Random"/> class.
    /// </summary>
    public class MewdekoRandom : Random
    {
        /// <summary>
        /// The <see cref="RandomNumberGenerator"/> instance used for generating random numbers.
        /// </summary>
        private readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

        /// <summary>
        /// Generates a non-negative random integer.
        /// </summary>
        /// <returns>A non-negative random integer.</returns>
        public override int Next()
        {
            var bytes = new byte[sizeof(int)];
            rng.GetBytes(bytes);
            // Use the absolute value in a safe manner, avoiding int.MinValue issue.
            var value = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
            return value;
        }

        /// <summary>
        /// Generates a non-negative random integer that is less than the specified maximum.
        /// </summary>
        /// <param name="maxValue">The exclusive upper bound of the random number to be generated.</param>
        /// <returns>A non-negative random integer that is less than <paramref name="maxValue"/>.</returns>
        public override int Next(int maxValue)
        {
            if (maxValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be positive.");

            // Generating a random int and using the double division method to ensure uniform distribution
            var result = Next();
            return (int)(maxValue * (result / (double)int.MaxValue));
        }

        /// <summary>
        /// Generates a random integer that is within a specified range.
        /// </summary>
        /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
        /// <param name="maxValue">The exclusive upper bound of the random number returned.</param>
        /// <returns>A random integer that is greater than or equal to <paramref name="minValue"/>, and less than <paramref name="maxValue"/>.</returns>
        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException(nameof(maxValue),
                    "minValue must be less than or equal to maxValue.");

            if (minValue == maxValue) return minValue;

            var range = (long)maxValue - minValue;
            return (int)(range * (Next() / (double)int.MaxValue)) + minValue;
        }

        /// <summary>
        /// Fills the elements of a specified array of bytes with random numbers.
        /// </summary>
        /// <param name="buffer">An array of bytes to contain random numbers.</param>
        public override void NextBytes(byte[] buffer) => rng.GetBytes(buffer);

        /// <summary>
        /// Returns a random floating-point number that is greater than or equal to 0.0, and less than 1.0.
        /// </summary>
        /// <returns>A double-precision floating point number that is greater than or equal to 0.0, and less than 1.0.</returns>
        protected override double Sample()
        {
            // Ensuring the Sample method returns a double in [0, 1).
            var bytes = new byte[sizeof(uint)];
            rng.GetBytes(bytes);
            var value = BitConverter.ToUInt32(bytes, 0);
            // Cast uint.MaxValue to double before adding 1 to avoid overflow.
            return value / ((double)uint.MaxValue + 1);
        }

        /// <summary>
        /// Returns a random floating-point number that is greater than or equal to 0.0, and less than 1.0.
        /// </summary>
        /// <returns>A double-precision floating point number that is greater than or equal to 0.0, and less than 1.0.</returns>
        public override double NextDouble()
        {
            // Utilizing the corrected Sample method to ensure uniform distribution.
            return Sample();
        }
    }
}