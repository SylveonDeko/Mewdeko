using NadekoBot.Common;
using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Core.Services
{
    public interface IBotConfigProvider
    {
        BotConfig BotConfig { get; }
        void Reload();
        bool Edit(BotConfigEditType type, string newValue);
        string GetValue(string name);
    }
}
