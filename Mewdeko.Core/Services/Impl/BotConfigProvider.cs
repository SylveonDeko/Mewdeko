using System.Reflection;
using Mewdeko.Common;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Impl
{
    public class BotConfigProvider : IBotConfigProvider
    {
        private readonly IDataCache _cache;
        private readonly DbService _db;

        public BotConfigProvider(DbService db, IDataCache cache)
        {
            _db = db;
            _cache = cache;
            Reload();
        }

        public BotConfig BotConfig { get; private set; }

        public void Reload()
        {
            using (var uow = _db.GetDbContext())
            {
                BotConfig = uow.BotConfig.GetOrCreate();
            }
        }

        public bool Edit(BotConfigEditType type, string newValue)
        {
            using (var uow = _db.GetDbContext())
            {
                var bc = uow.BotConfig.GetOrCreate();
                switch (type)
                {
                    case BotConfigEditType.CurrencyName:
                        bc.CurrencyName = newValue ?? "-";
                        break;
                    case BotConfigEditType.CurrencySign:
                        bc.CurrencySign = newValue ?? "-";
                        break;
                    case BotConfigEditType.DailyCurrencyDecay:
                        if (float.TryParse(newValue, out var decay) && decay >= 0)
                            bc.DailyCurrencyDecay = decay;
                        else
                            return false;
                        break;
                    case BotConfigEditType.CurrencyGenerationChance:
                        if (float.TryParse(newValue, out var chance)
                            && chance >= 0
                            && chance <= 1)
                            bc.CurrencyGenerationChance = chance;
                        else
                            return false;
                        break;
                    case BotConfigEditType.CurrencyGenerationCooldown:
                        if (int.TryParse(newValue, out var cd) && cd >= 1)
                            bc.CurrencyGenerationCooldown = cd;
                        else
                            return false;
                        break;
                    case BotConfigEditType.CurrencyDropAmount:
                        if (int.TryParse(newValue, out var amount) && amount > 0)
                            bc.CurrencyDropAmount = amount;
                        else
                            return false;
                        break;
                    case BotConfigEditType.CurrencyDropAmountMax:
                        if (newValue == null)
                            bc.CurrencyDropAmountMax = null;
                        else if (int.TryParse(newValue, out var maxAmount) && maxAmount > 0)
                            bc.CurrencyDropAmountMax = maxAmount;
                        else
                            return false;
                        break;
                    case BotConfigEditType.PatreonCurrencyPerCent:
                        if (float.TryParse(newValue, out var cents) && cents > 0)
                            bc.PatreonCurrencyPerCent = cents;
                        else
                            return false;
                        break;
                    case BotConfigEditType.TriviaCurrencyReward:
                        if (int.TryParse(newValue, out var triviaReward) && triviaReward >= 0)
                            bc.TriviaCurrencyReward = triviaReward;
                        else
                            return false;
                        break;
                    case BotConfigEditType.Betroll100Multiplier:
                        if (float.TryParse(newValue, out var br100) && br100 > 0)
                            bc.Betroll100Multiplier = br100;
                        else
                            return false;
                        break;
                    case BotConfigEditType.Betroll91Multiplier:
                        if (int.TryParse(newValue, out var br91) && br91 > 0)
                            bc.Betroll91Multiplier = br91;
                        else
                            return false;
                        break;
                    case BotConfigEditType.Betroll67Multiplier:
                        if (int.TryParse(newValue, out var br67) && br67 > 0)
                            bc.Betroll67Multiplier = br67;
                        else
                            return false;
                        break;
                    case BotConfigEditType.BetflipMultiplier:
                        if (float.TryParse(newValue, out var bf) && bf > 0)
                            bc.BetflipMultiplier = bf;
                        else
                            return false;
                        break;
                    case BotConfigEditType.XpPerMessage:
                        if (int.TryParse(newValue, out var xp) && xp > 0)
                            bc.XpPerMessage = xp;
                        else
                            return false;
                        break;
                    case BotConfigEditType.XpMinutesTimeout:
                        if (int.TryParse(newValue, out var min) && min > 0)
                            bc.XpMinutesTimeout = min;
                        else
                            return false;
                        break;
                    case BotConfigEditType.VoiceXpPerMinute:
                        if (double.TryParse(newValue, out var rate) && rate >= 0)
                            bc.VoiceXpPerMinute = rate;
                        else
                            return false;
                        break;
                    case BotConfigEditType.MaxXpMinutes:
                        if (int.TryParse(newValue, out var minutes) && minutes > 0)
                            bc.MaxXpMinutes = minutes;
                        else
                            return false;
                        break;
                    case BotConfigEditType.MinWaifuPrice:
                        if (int.TryParse(newValue, out var price) && price > 0)
                            bc.MinWaifuPrice = price;
                        else
                            return false;
                        break;
                    case BotConfigEditType.WaifuGiftMultiplier:
                        if (int.TryParse(newValue, out var mult) && mult > 0)
                            bc.WaifuGiftMultiplier = mult;
                        else
                            return false;
                        break;
                    case BotConfigEditType.MinimumTriviaWinReq:
                        if (int.TryParse(newValue, out var req) && req >= 0)
                            bc.MinimumTriviaWinReq = req;
                        else
                            return false;
                        break;
                    case BotConfigEditType.MinBet:
                        if (int.TryParse(newValue, out var gmin) && gmin >= 0)
                            bc.MinBet = gmin;
                        else
                            return false;
                        break;
                    case BotConfigEditType.MaxBet:
                        if (int.TryParse(newValue, out var gmax) && gmax >= 0)
                            bc.MaxBet = gmax;
                        else
                            return false;
                        break;
                    case BotConfigEditType.CurrencyGenerationPassword:
                        if (!bool.TryParse(newValue, out var pw))
                            return false;
                        bc.CurrencyGenerationPassword = pw;
                        break;
                    default:
                        return false;
                }

                BotConfig = bc;
                uow.SaveChanges();
            }

            return true;
        }

        public string GetValue(string name)
        {
            var value = typeof(BotConfig)
                .GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(BotConfig);
            return value?.ToString() ?? "-";
        }
    }
}