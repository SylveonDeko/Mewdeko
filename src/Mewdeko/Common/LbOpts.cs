using CommandLine;

namespace Mewdeko.Common;

public class LbOpts : IMewdekoCommandOptions
{
    [Option('c', "clean", Default = false, HelpText = "Only show users who are on the server.")]
    public bool Clean { get; set; }

    public void NormalizeOptions()
    {
    }
}