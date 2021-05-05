using System.Threading.Tasks;

namespace NadekoBot.Modules.Games.Common.ChatterBot
{
    public interface IChatterBotSession
    {
        Task<string> Think(string input);
    }
}
