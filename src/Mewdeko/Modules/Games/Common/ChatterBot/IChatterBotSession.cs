using System.Threading.Tasks;

namespace Mewdeko.Modules.Games.Common.ChatterBot;

public interface IChatterBotSession
{
    Task<string> Think(string input);
}