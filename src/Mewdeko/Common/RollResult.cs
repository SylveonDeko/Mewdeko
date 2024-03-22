namespace Mewdeko.Common
{
    /// <summary>
    /// Represents the result of rolling multiple dice.
    /// </summary>
    public class RollResult
    {
        /// <summary>
        /// Gets or sets the dictionary containing the results of each die rolled.
        /// </summary>
        public Dictionary<Die, List<int>>? Results { get; set; }

        /// <summary>
        /// Gets or sets the total sum of all dice rolled.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the total is inaccurate.
        /// </summary>
        public bool InacurateTotal { get; set; }

        /// <summary>
        /// Returns a string representation of the roll result, displaying the total.
        /// </summary>
        /// <returns>A string representation of the roll result.</returns>
        public override string ToString() => $"Total: **{Total.ToString()}**";

        /// <summary>
        /// Initializes a new instance of the RollResult class.
        /// </summary>
        public RollResult() => Results = new Dictionary<Die, List<int>>();
    }
}