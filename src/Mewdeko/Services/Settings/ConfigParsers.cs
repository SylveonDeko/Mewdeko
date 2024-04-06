using System.Globalization;
using SkiaSharp;

namespace Mewdeko.Services.Settings
{
    /// <summary>
    /// Custom setting value parsers for types which don't have them by default
    /// </summary>
    public static class ConfigParsers
    {
        /// <summary>
        /// Default string parser. Passes input to output and returns true.
        /// </summary>
        public static bool String(string input, out string output)
        {
            output = input;
            return true;
        }

        /// <summary>
        /// Parses the input string into a CultureInfo object.
        /// </summary>
        /// <param name="input">The input string representing a culture.</param>
        /// <param name="output">The parsed CultureInfo object.</param>
        /// <returns>True if parsing is successful, otherwise false.</returns>
        public static bool Culture(string input, out CultureInfo output)
        {
            try
            {
                output = new CultureInfo(input);
                return true;
            }
            catch
            {
                output = null;
                return false;
            }
        }
    }

    /// <summary>
    /// Custom setting value printers for types which don't have them by default
    /// </summary>
    public static class ConfigPrinters
    {
        /// <summary>
        /// Converts the input object to its string representation.
        /// </summary>
        /// <typeparam name="TAny">The type of the input object.</typeparam>
        /// <param name="input">The input object.</param>
        /// <returns>The string representation of the input object, or "null" if the input is null.</returns>
        public static string ToString<TAny>(TAny? input) => input?.ToString() ?? "null";

        /// <summary>
        /// Converts the SKColor to its hexadecimal representation.
        /// </summary>
        /// <param name="color">The SKColor to convert.</param>
        /// <returns>The hexadecimal representation of the SKColor.</returns>
        public static string Color(SKColor color) =>
            ((uint)((color.Blue << 0) | (color.Green << 8) | (color.Red << 16))).ToString("X6");

        /// <summary>
        /// Converts the CultureInfo to its name.
        /// </summary>
        /// <param name="culture">The culture to convert</param>
        /// <returns>The culture in string form</returns>
        public static string Culture(CultureInfo culture) => culture.Name;
    }
}