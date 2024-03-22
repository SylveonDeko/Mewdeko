using CommandLine;

namespace Mewdeko.Common
{
    /// <summary>
    /// A static class responsible for parsing command-line options.
    /// </summary>
    public static class OptionsParser
    {
        /// <summary>
        /// Parses command-line arguments into an instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of options to parse.</typeparam>
        /// <param name="args">The command-line arguments to parse.</param>
        /// <returns>An instance of type <typeparamref name="T"/> representing the parsed options.</returns>
        public static T ParseFrom<T>(string[] args) where T : IMewdekoCommandOptions, new() =>
            ParseFrom(new T(), args).Item1;

        /// <summary>
        /// Parses command-line arguments into the provided <paramref name="options"/> instance.
        /// </summary>
        /// <typeparam name="T">The type of options to parse.</typeparam>
        /// <param name="options">The instance of options to populate.</param>
        /// <param name="args">The command-line arguments to parse.</param>
        /// <returns>
        /// A tuple containing the populated <paramref name="options"/> instance and a boolean indicating
        /// whether the parsing was successful.
        /// </returns>
        public static (T, bool) ParseFrom<T>(T options, string[] args) where T : IMewdekoCommandOptions
        {
            using var p = new Parser(x => x.HelpWriter = null);
            var res = p.ParseArguments<T>(args);
            var options1 = options;
            options = res.MapResult(x => x, _ => options1);
            options.NormalizeOptions();
            return (options, res.Tag == ParserResultType.Parsed);
        }
    }
}