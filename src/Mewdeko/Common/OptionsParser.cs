using CommandLine;

namespace Mewdeko.Common;

public static class OptionsParser
{
    public static T ParseFrom<T>(string[] args) where T : IMewdekoCommandOptions, new() => ParseFrom(new T(), args).Item1;

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