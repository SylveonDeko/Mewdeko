using CommandLine;

namespace NadekoBot.Core.Common
{
    public class LbOpts : INadekoCommandOptions
    {
        [Option('c', "clean", Default = false, HelpText = "Only show users who are on the server.")]
        public bool Clean { get; set; }
        public void NormalizeOptions()
        {

        }
    }
}
