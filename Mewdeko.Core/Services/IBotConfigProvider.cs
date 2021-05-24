using Mewdeko.Common;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services
{
    public interface IBotConfigProvider
    {
        BotConfig BotConfig { get; }
        void Reload();
        bool Edit(BotConfigEditType type, string newValue);
        string GetValue(string name);
    }
}