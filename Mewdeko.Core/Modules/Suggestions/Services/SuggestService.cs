using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Replacements;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Suggestions.Services
{
    public class SuggestionsService : INService
    {
        public readonly DbService _db;

        public CommandHandler CmdHandler;
        public DiscordSocketClient _client;
        public Administration.Services.AdministrationService adminserv;
        public SuggestionsService(DbService db, Mewdeko bot, CommandHandler cmd, DiscordSocketClient client, Administration.Services.AdministrationService aserv)
        {
            adminserv = aserv;
            CmdHandler = cmd;
            _client = client;
            _client.MessageReceived += MessageRecieved;
            _db = db;
            _snum = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.sugnum)
                .ToConcurrent();
            _sugchans = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.sugchan)
                .ToConcurrent();
            _sugroles = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.SuggestRole)
                .ToConcurrent();
            _suggestmsgs = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.SuggestMessage)
                .ToConcurrent();
            _acceptmsgs = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.AcceptMessage)
                .ToConcurrent();
            _denymsgs = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.DenyMessage)
                .ToConcurrent();
            _implementmsgs = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.ImplementMessage)
                .ToConcurrent();
            _considermsgs = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.ConsiderMessage)
                .ToConcurrent();
            _suggestemotes = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.SuggestEmotes)
                .ToConcurrent();
        }
        private ConcurrentDictionary<ulong, string> _suggestemotes { get; } = new();
        private ConcurrentDictionary<ulong, ulong> _sugchans { get; } = new();
        private ConcurrentDictionary<ulong, ulong> _sugroles { get; } = new();
        private ConcurrentDictionary<ulong, ulong> _snum { get; } = new();
        private ConcurrentDictionary<ulong, string> _suggestmsgs { get; } = new();
        private ConcurrentDictionary<ulong, string> _acceptmsgs { get; } = new();
        private ConcurrentDictionary<ulong, string> _denymsgs { get; } = new();
        private ConcurrentDictionary<ulong, string> _implementmsgs { get; } = new();
        private ConcurrentDictionary<ulong, string> _considermsgs { get; } = new();
        public async Task MessageRecieved(SocketMessage msg)
        {
            if (msg.Channel is not SocketGuildChannel chan) return;
            var Guild = (msg.Channel as IGuildChannel).Guild;
            var Prefix = CmdHandler.GetPrefix(Guild);
            if (msg.Channel.Id == GetSuggestionChannel(Guild.Id) && msg.Author.IsBot == false &&
                !msg.Content.StartsWith(Prefix))
            {
                if (msg.Channel is SocketThreadChannel) return;
                var guser = msg.Author as IGuildUser;
                if (guser.RoleIds.Contains(adminserv.GetStaffRole(guser.Guild.Id))) return;
                await SendSuggestion(chan.Guild, msg.Author as IGuildUser, _client, msg.Content, msg.Channel as ITextChannel);
                try { await msg.DeleteAsync(); } catch { } 
            }
        }
        public ulong GetSNum(ulong? id)
        {
            _snum.TryGetValue(id.Value, out var snum);
            return snum;
        }
        public string GetEmotes(ulong? id)
        {
            _suggestemotes.TryGetValue(id.Value, out var smotes);
            return smotes;
        }
        public async Task SetSuggestionEmotes(IGuild guild, string parsedEmotes)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.SuggestEmotes = parsedEmotes;
                await uow.SaveChangesAsync();
            }

            _suggestemotes.AddOrUpdate(guild.Id, parsedEmotes, (key, old) => parsedEmotes);
        }
        public async Task SetSuggestionChannelId(IGuild guild, ulong channel)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.sugchan = channel;
                await uow.SaveChangesAsync();
            }

            _sugchans.AddOrUpdate(guild.Id, channel, (key, old) => channel);
        }
        public async Task SetSuggestionMessage(IGuild guild, string message)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.SuggestMessage = message;
                await uow.SaveChangesAsync();
            }

            _suggestmsgs.AddOrUpdate(guild.Id, message, (key, old) => message);
        }
        public async Task SetAcceptMessage(IGuild guild, string message)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.AcceptMessage = message;
                await uow.SaveChangesAsync();
            }

            _acceptmsgs.AddOrUpdate(guild.Id, message, (key, old) => message);
        }
        public async Task SetDenyMessage(IGuild guild, string message)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.DenyMessage = message;
                await uow.SaveChangesAsync();
            }

            _denymsgs.AddOrUpdate(guild.Id, message, (key, old) => message);
        }
        public async Task SetImplementMessage(IGuild guild, string message)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.ImplementMessage = message;
                await uow.SaveChangesAsync();
            }

            _implementmsgs.AddOrUpdate(guild.Id, message, (key, old) => message);
        }
        public async Task SetConsiderMessage(IGuild guild, string message)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.ConsiderMessage = message;
                await uow.SaveChangesAsync();
            }

            _considermsgs.AddOrUpdate(guild.Id, message, (key, old) => message);
        }
        public async Task SetSuggestionRole(IGuild guild, ulong name)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.SuggestRole = name;
                await uow.SaveChangesAsync();
            }

            _sugroles.AddOrUpdate(guild.Id, name, (key, old) => name);
        }

        public async Task sugnum(IGuild guild, ulong num)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.sugnum = num;
                await uow.SaveChangesAsync();
            }

            _snum.AddOrUpdate(guild.Id, num, (key, old) => num);
        }

        public ulong GetSuggestionChannel(ulong? id)
        {
            if (id == null || !_sugchans.TryGetValue(id.Value, out var SugChan))
                return 0;

            return SugChan;
        }
        public string GetSuggestionMessage(IGuild guild)
        {
            _suggestmsgs.TryGetValue(guild.Id, out var snum);
            if (snum == "")
                return "";
            else return snum;
        }
        public string GetAcceptMessage(IGuild guild)
        {
            _acceptmsgs.TryGetValue(guild.Id, out var snum);
            if (snum == "")
                return "";
            else return snum;
        }
        public string GetDenyMessage(IGuild guild)
        {
            _denymsgs.TryGetValue(guild.Id, out var snum);
            if (snum == "")
                return "";
            else return snum;
        }
        public string GetImplementMessage(IGuild guild)
        {
            _implementmsgs.TryGetValue(guild.Id, out var snum);
            if (snum == "")
                return "";
            else return snum;
        }
        public string GetConsiderMessage(IGuild guild)
        {
            _considermsgs.TryGetValue(guild.Id, out var snum);
            if (snum == "")
                return "";
            else return snum;
        }
        public ulong GetSuggestionRole(ulong? id)
        {
            if (id == null || !_sugroles.TryGetValue(id.Value, out var SugRole))
                return 0;

            return SugRole;
        }
        public async Task SendDenyEmbed(IGuild guild, DiscordSocketClient client, IUser user, ulong suggestion, ITextChannel channel, string reason = null)
        {
            string rs;
            if (reason == null)
                rs = "none";
            else
                rs = reason;
            var suggest = Suggestionse(guild.Id, suggestion).FirstOrDefault();
            var use = await guild.GetUserAsync(suggest.UserID);
            if (suggest is null)
            {
                await channel.SendErrorAsync("That suggestion number doesnt exist! Please double check it exists and try again.");
                return;
            }
            var eb = new EmbedBuilder();
            var e = GetDenyMessage(guild);
            if (GetDenyMessage(guild) == "-" || GetDenyMessage(guild) == "" || GetDenyMessage(guild) == null)
            {
                if (suggest.Suggestion != null)
                {
                    eb = new EmbedBuilder()
                            .WithAuthor(use)
                            .WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Denied")
                            .WithDescription(suggest.Suggestion)
                            .WithOkColor()
                            .AddField("Reason", rs);
                }
                else
                {
                    var desc = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)).Result.GetMessageAsync(suggest.MessageID);
                    eb = new EmbedBuilder()
                           .WithAuthor(use)
                           .WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Denied")
                           .WithDescription(desc.Embeds.FirstOrDefault().Description)
                           .WithOkColor()
                           .AddField("Reason", rs);
                }
                var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
                var message = chan.GetMessageAsync(suggest.MessageID, CacheMode.AllowDownload).Result as IUserMessage;
                try { await message.RemoveAllReactionsAsync(); } catch { }
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                });
                try
                {
                    var emb = new EmbedBuilder();
                    emb.WithAuthor(use);
                    emb.WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Denied");
                    emb.WithDescription(suggest.Suggestion);
                    emb.AddField("Reason", rs);
                    emb.AddField("Denied By", user);
                    emb.WithErrorColor();
                    await guild.GetUserAsync(suggest.UserID).Result.SendMessageAsync(embed: emb.Build());
                    await channel.SendConfirmAsync("Suggestion set as denied and the user has been dmed the denial!");
                }
                catch
                {
                    await channel.SendConfirmAsync("Suggestion set as denied but the user had their dms off.");
                }
            }
            else
            {
                string sug;
                if (suggest.Suggestion == null)
                    sug = guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)).Result.GetMessageAsync(suggest.MessageID).Result.Embeds.FirstOrDefault().Description;
                else
                    sug = suggest.Suggestion;
                var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
                var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
                CREmbed crEmbed = null;
                var sugnum1 = GetSNum(guild.Id);
                var suguse = await guild.GetUserAsync(suggest.UserID);
                var replacer = new ReplacementBuilder()
                 .WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", () => suguse.ToString())
                .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                .WithOverride("%suggest.message%", () => sug)
                .WithOverride("%suggest.number%", () => suggest.SuggestID.ToString())
                .WithOverride("%suggest.user.name%", () => suguse.Username)
                .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl(2048).ToString())
                .WithOverride("%suggest.mod.user%", () => user.ToString())
                .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl(2048).ToString())
                .WithOverride("%suggest.mod.name%", () => user.Username)
                .WithOverride("%suggest.mod.message%", () => rs)
                .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                .Build();
                var ebe = CREmbed.TryParse(GetDenyMessage(guild), out crEmbed);
                if (ebe is false)
                {
                    await channel.SendErrorAsync("The deny message is invalid, I have set it back to default to avoid further issues. Please set it again and try again. If you are having an issue please visit the suport server shown when you mention Mewdeko.");
                    await SetDenyMessage(guild, "-");
                    return;
                }
                replacer.Replace(crEmbed);
                if (crEmbed.PlainText != null && crEmbed.IsEmbedValid)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = crEmbed.PlainText.SanitizeAllMentions();
                        x.Embed = crEmbed.ToEmbed().Build();
                    });
                }
                if (crEmbed.PlainText is null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = null;
                        x.Embed = crEmbed.ToEmbed().Build();
                    });
                }
                if (crEmbed.PlainText != null && !crEmbed.IsEmbedValid)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = crEmbed.PlainText.SanitizeAllMentions();
                        x.Embed = null;
                    });
                }
                if (crEmbed.PlainText is null && !crEmbed.IsEmbedValid)
                {
                    await channel.SendErrorAsync("The deny message is invalid, please set it again and try again. If you are having an issue please visit the suport server shown when you mention Mewdeko.");
                    return;
                }
                try
                {
                    var emb = new EmbedBuilder();
                    emb.WithAuthor(use);
                    emb.WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Denied");
                    emb.WithDescription(suggest.Suggestion);
                    emb.AddField("Reason", rs);
                    emb.AddField("Denied By", user);
                    emb.WithOkColor();
                    await guild.GetUserAsync(suggest.UserID).Result.SendMessageAsync(embed: emb.Build());
                    await channel.SendConfirmAsync("Suggestion set as denied and the user has been dmed the denial!");
                }
                catch
                {
                    await channel.SendConfirmAsync("Suggestion set as denied but the user had their dms off.");
                }
            }
        }
        public async Task SendConsiderEmbed(IGuild guild, DiscordSocketClient client, IUser user, ulong suggestion, ITextChannel channel, string reason = null)
        {
            string rs;
            if (reason == null)
                rs = "none";
            else
                rs = reason;
            var suggest = Suggestionse(guild.Id, suggestion).FirstOrDefault();
            var use = await guild.GetUserAsync(suggest.UserID);
            if (suggest is null)
            {
                await channel.SendErrorAsync("That suggestion number doesnt exist! Please double check it exists and try again.");
                return;
            }
            var eb = new EmbedBuilder();
            var e = GetConsiderMessage(guild);
            if (GetConsiderMessage(guild) == "-" || GetConsiderMessage(guild) == "" || GetConsiderMessage(guild) == null)
            {
                if (suggest.Suggestion != null)
                {
                    eb = new EmbedBuilder()
                            .WithAuthor(use)
                            .WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Considering")
                            .WithDescription(suggest.Suggestion)
                            .WithOkColor()
                            .AddField("Reason", rs);
                }
                else
                {
                    var desc = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)).Result.GetMessageAsync(suggest.MessageID);
                    eb = new EmbedBuilder()
                           .WithAuthor(use)
                           .WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Considering")
                           .WithDescription(desc.Embeds.FirstOrDefault().Description)
                           .WithOkColor()
                           .AddField("Reason", rs);
                }
                var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
                var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
                try { await message.RemoveAllReactionsAsync(); } catch { }
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                });
                try
                {
                    var emb = new EmbedBuilder();
                    emb.WithAuthor(use);
                    emb.WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Considering");
                    emb.WithDescription(suggest.Suggestion);
                    emb.AddField("Reason", rs);
                    emb.AddField("Denied By", user);
                    emb.WithOkColor();
                    await guild.GetUserAsync(suggest.UserID).Result.SendMessageAsync(embed: emb.Build());
                    await channel.SendConfirmAsync("Suggestion set as considering and the user has been dmed the consideration!");
                }
                catch
                {
                    await channel.SendConfirmAsync("Suggestion set as considering but the user had their dms off.");
                }
            }
            else
            {
                string sug;
                if (suggest.Suggestion == null)
                    sug = guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)).Result.GetMessageAsync(suggest.MessageID).Result.Embeds.FirstOrDefault().Description;
                else
                    sug = suggest.Suggestion;
                var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
                var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
                CREmbed crEmbed = null;
                var sugnum1 = GetSNum(guild.Id);
                var suguse = await guild.GetUserAsync(suggest.UserID);
                var replacer = new ReplacementBuilder()
                .WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", () => suguse.ToString())
                .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                .WithOverride("%suggest.message%", () => sug)
                .WithOverride("%suggest.number%", () => suggest.SuggestID.ToString())
                .WithOverride("%suggest.user.name%", () => suguse.Username)
                .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl(2048).ToString())
                .WithOverride("%suggest.mod.user%", () => user.ToString())
                .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl(2048).ToString())
                .WithOverride("%suggest.mod.name%", () => user.Username)
                .WithOverride("%suggest.mod.message%", () => rs)
                .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                .Build();
                var ebe = CREmbed.TryParse(GetConsiderMessage(guild), out crEmbed);
                if (ebe is false)
                {
                    await channel.SendErrorAsync("The consider message is invalid, I have set it back to default to avoid further issues. Please set it again and try again. If you are having an issue please visit the suport server shown when you mention Mewdeko.");
                    await SetConsiderMessage(guild, "-");
                    return;
                }
                replacer.Replace(crEmbed);
                if (crEmbed.PlainText != null && crEmbed.IsEmbedValid)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = crEmbed.PlainText.SanitizeAllMentions();
                        x.Embed = crEmbed.ToEmbed().Build();
                    });
                }
                if (crEmbed.PlainText is null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = null;
                        x.Embed = crEmbed.ToEmbed().Build();
                    });
                }
                if (crEmbed.PlainText != null && !crEmbed.IsEmbedValid)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = crEmbed.PlainText.SanitizeAllMentions();
                        x.Embed = null;
                    });
                }
                if (crEmbed.PlainText is null && !crEmbed.IsEmbedValid)
                {
                    await channel.SendErrorAsync("The consider message set is invalid, please set it again and try again. If you are having an issue please visit the suport server shown when you mention Mewdeko.");
                    return;
                }
                try
                {
                    var emb = new EmbedBuilder();
                    emb.WithAuthor(use);
                    emb.WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Considering");
                    emb.WithDescription(suggest.Suggestion);
                    emb.AddField("Reason", rs);
                    emb.AddField("Considered by", user);
                    emb.WithOkColor();
                    await guild.GetUserAsync(suggest.UserID).Result.SendMessageAsync(embed: emb.Build());
                    await channel.SendConfirmAsync("Suggestion set as considering and the user has been dmed the consideration!");
                }
                catch
                {
                    await channel.SendConfirmAsync("Suggestion set as considering but the user had their dms off.");
                }
            }
        }
        public async Task SendImplementEmbed(IGuild guild, DiscordSocketClient client, IUser user, ulong suggestion, ITextChannel channel, string reason = null)
        {
            string rs;
            if (reason == null)
                rs = "none";
            else
                rs = reason;
            var suggest = Suggestionse(guild.Id, suggestion).FirstOrDefault();
            var use = await guild.GetUserAsync(suggest.UserID);
            if (suggest is null)
            {
                await channel.SendErrorAsync("That suggestion number doesnt exist! Please double check it exists and try again.");
                return;
            }
            var eb = new EmbedBuilder();
            if (GetImplementMessage(guild) == "-" || GetImplementMessage(guild) == "" || GetImplementMessage(guild) == null)
            {
                if (suggest.Suggestion != null)
                {
                    eb = new EmbedBuilder()
                            .WithAuthor(use)
                            .WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Implemented")
                            .WithDescription(suggest.Suggestion)
                            .WithOkColor()
                            .AddField("Reason", rs);
                }
                else
                {
                    var desc = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)).Result.GetMessageAsync(suggest.MessageID);
                    eb = new EmbedBuilder()
                           .WithAuthor(use)
                           .WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Implemented")
                           .WithDescription(desc.Embeds.FirstOrDefault().Description)
                           .WithOkColor()
                           .AddField("Reason", rs);
                }
                var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
                var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                });
                try { await message.RemoveAllReactionsAsync(); } catch { }
                try
                {
                    var emb = new EmbedBuilder();
                    emb.WithAuthor(use);
                    emb.WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Implemented");
                    emb.WithDescription(suggest.Suggestion);
                    emb.AddField("Reason", rs);
                    emb.AddField("Implemented By", user);
                    emb.WithOkColor();
                    await guild.GetUserAsync(suggest.UserID).Result.SendMessageAsync(embed: emb.Build());
                    await channel.SendConfirmAsync("Suggestion set as implemented and the user has been dmed that!");
                }
                catch
                {
                    await channel.SendConfirmAsync("Suggestion set as implemented but the user had their dms off.");
                }
            }
            else
            {
                string sug;
                if (suggest.Suggestion == null)
                    sug = guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)).Result.GetMessageAsync(suggest.MessageID).Result.Embeds.FirstOrDefault().Description;
                else
                    sug = suggest.Suggestion;
                var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
                var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
                CREmbed crEmbed = null;
                var sugnum1 = GetSNum(guild.Id);
                var suguse = await guild.GetUserAsync(suggest.UserID);
                var replacer = new ReplacementBuilder()
                 .WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", () => suguse.ToString())
                .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                .WithOverride("%suggest.message%", () => sug)
                .WithOverride("%suggest.number%", () => suggest.SuggestID.ToString())
                .WithOverride("%suggest.user.name%", () => suguse.Username)
                .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl(2048).ToString())
                .WithOverride("%suggest.mod.user%", () => user.ToString())
                .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl(2048).ToString())
                .WithOverride("%suggest.mod.name%", () => user.Username)
                .WithOverride("%suggest.mod.message%", () => rs)
                .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                .Build();
                var ebe = CREmbed.TryParse(GetImplementMessage(guild), out crEmbed);
                if (ebe is false)
                {
                    await channel.SendErrorAsync("The implement message set is invalid, I have set it back to default to avoid further issues. Please set it again and try again. If you are having an issue please visit the suport server shown when you mention Mewdeko.");
                    await SetImplementMessage(guild, "-");
                    return;
                }
                replacer.Replace(crEmbed);
                if (crEmbed.PlainText != null && crEmbed.IsEmbedValid)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = crEmbed.PlainText.SanitizeAllMentions();
                        x.Embed = crEmbed.ToEmbed().Build();
                    });
                }
                if (crEmbed.PlainText is null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = null;
                        x.Embed = crEmbed.ToEmbed().Build();
                    });
                }
                if (crEmbed.PlainText != null && !crEmbed.IsEmbedValid)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = crEmbed.PlainText.SanitizeAllMentions();
                        x.Embed = null;
                    });
                }
                if(crEmbed.PlainText is null && !crEmbed.IsEmbedValid)
                {
                    await channel.SendErrorAsync("The implement message is invalid, please set it again and try again. If you are having an issue please visit the suport server shown when you mention Mewdeko.");
                    return;
                }
                try
                {
                    var emb = new EmbedBuilder();
                    emb.WithAuthor(use);
                    emb.WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Implemented");
                    emb.WithDescription(suggest.Suggestion);
                    emb.AddField("Reason", rs);
                    emb.AddField("Implemented By", user);
                    emb.WithOkColor();
                    await guild.GetUserAsync(suggest.UserID).Result.SendMessageAsync(embed: emb.Build());
                    await channel.SendConfirmAsync("Suggestion set as implemented and the user has been dmed that!");
                }
                catch
                {
                    await channel.SendConfirmAsync("Suggestion set as implemented but the user had their dms off.");
                }
            }
        }
        public async Task SendAcceptEmbed(IGuild guild, DiscordSocketClient client, IUser user, ulong suggestion, ITextChannel channel, string reason = null)
        {
            string rs;
            if (reason == null)
                rs = "none";
            else
                rs = reason;
            var suggest = Suggestionse(guild.Id, suggestion).FirstOrDefault();
            var use = await guild.GetUserAsync(suggest.UserID);
            if (suggest is null)
            {
                await channel.SendErrorAsync("That suggestion number doesnt exist! Please double check it exists and try again.");
                return;
            }
            var eb = new EmbedBuilder();
            var e = GetAcceptMessage(guild);
            if (GetAcceptMessage(guild) == "-" || GetAcceptMessage(guild) == "" || GetAcceptMessage(guild) == null)
            {
                if (suggest.Suggestion != null)
                {
                    eb = new EmbedBuilder()
                            .WithAuthor(use)
                            .WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Accepted")
                            .WithDescription(suggest.Suggestion)
                            .WithOkColor()
                            .AddField("Reason", rs);
                }
                else
                {
                    var desc = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)).Result.GetMessageAsync(suggest.MessageID);
                    eb = new EmbedBuilder()
                           .WithAuthor(use)
                           .WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Accepted")
                           .WithDescription(desc.Embeds.FirstOrDefault().Description)
                           .WithOkColor()
                           .AddField("Reason", rs);
                }
                var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
                var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                });
                try { await message.RemoveAllReactionsAsync(); } catch { }
                try
                {
                    var emb = new EmbedBuilder();
                    emb.WithAuthor(use);
                    emb.WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Accepted");
                    emb.WithDescription(suggest.Suggestion);
                    emb.AddField("Reason", rs);
                    emb.AddField("Accepted By", user);
                    emb.WithOkColor();
                    await guild.GetUserAsync(suggest.UserID).Result.SendMessageAsync(embed: emb.Build());
                    await channel.SendConfirmAsync("Suggestion set as accepted and the user has been dmed the acceptance!");
                }
                catch
                {
                    await channel.SendConfirmAsync("Suggestion set as accepted but the user had their dms off.");
                }
            }
            else
            {
                string sug;
                if (suggest.Suggestion == null)
                    sug = guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)).Result.GetMessageAsync(suggest.MessageID).Result.Embeds.FirstOrDefault().Description;
                else
                    sug = suggest.Suggestion;
                var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
                var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
                CREmbed crEmbed = null;
                var sugnum1 = GetSNum(guild.Id);
                var suguse = await guild.GetUserAsync(suggest.UserID);
                var replacer = new ReplacementBuilder()
                 .WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", () => suguse.ToString())
                .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                .WithOverride("%suggest.message%", () => sug)
                .WithOverride("%suggest.number%", () => suggest.SuggestID.ToString())
                .WithOverride("%suggest.user.name%", () => suguse.Username)
                .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl(2048).ToString())
                .WithOverride("%suggest.mod.user%", () => user.ToString())
                .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl(2048).ToString())
                .WithOverride("%suggest.mod.name%", () => user.Username)
                .WithOverride("%suggest.mod.message%", () => rs)
                .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                .Build();
                var ebe = CREmbed.TryParse(GetAcceptMessage(guild), out crEmbed);
                if (ebe is false)
                {
                    await channel.SendErrorAsync("The accept message set is invalid, I have set it back to default to avoid further issues. Please set it again and try again. If you are having an issue please visit the suport server shown when you mention Mewdeko.");
                    await SetAcceptMessage(guild, "-");
                    return;
                }
                replacer.Replace(crEmbed);
                if (crEmbed.PlainText != null && crEmbed.IsEmbedValid)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = crEmbed.PlainText.SanitizeAllMentions();
                        x.Embed = crEmbed.ToEmbed().Build();
                    });
                }
                if (crEmbed.PlainText is null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = null;
                        x.Embed = crEmbed.ToEmbed().Build();
                    });
                }
                if (crEmbed.PlainText != null && !crEmbed.IsEmbedValid)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = crEmbed.PlainText.SanitizeAllMentions();
                        x.Embed = null;
                    });
                }
                if (crEmbed.PlainText is null && !crEmbed.IsEmbedValid)
                {
                    await channel.SendErrorAsync("The accept message is invalid, please set it again and try again. If you are having an issue please visit the suport server shown when you mention Mewdeko.");
                    return;
                }
                try
                {
                    var emb = new EmbedBuilder();
                    emb.WithAuthor(use);
                    emb.WithTitle($"Suggestion #{GetSNum(guild.Id) - 1} Accepted");
                    emb.WithDescription(suggest.Suggestion);
                    emb.AddField("Reason", rs);
                    emb.AddField("Accepted By", user);
                    emb.WithOkColor();
                    await guild.GetUserAsync(suggest.UserID).Result.SendMessageAsync(embed: emb.Build());
                    await channel.SendConfirmAsync("Suggestion set as accepted and the user has been dmed the acceptance!");
                }
                catch
                {
                    await channel.SendConfirmAsync("Suggestion set as accepted but the user had their dms off.");
                }
            }
        }
        public async Task SendSuggestion(IGuild guild, IGuildUser user, DiscordSocketClient client, string suggestion, ITextChannel channel)
        {
            if(GetSuggestionChannel(guild.Id) == 0)
            {
                var msg = await channel.SendErrorAsync("There is no suggestion channel set! Have an admin set it using `setsuggestchannel` and try again!");
                msg.DeleteAfter(3);
                return;
            }
            var tup = new Emoji("\uD83D\uDC4D");
            var tdown = new Emoji("\uD83D\uDC4E");
            var emotes = new List<Emote>();
            var em = GetEmotes(guild.Id);
            if (em != null && em != "disable")
            {
                var te = em.Split(",");
                foreach (var emote in te)
                {
                    emotes.Add(Emote.Parse(emote));
                }
            }
            if (GetSuggestionMessage(guild) == "-" || GetSuggestionMessage(guild) == "")
            {
                string n;
                if (GetSuggestionRole(guild.Id) != 0)
                {
                    var role = guild.GetRole(GetSuggestionRole(guild.Id));
                    n = role.Mention;
                }
                else
                {
                    n = "_ _";
                }

                var sugnum1 = GetSNum(guild.Id);
                var t = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).EmbedAsync(
                    new EmbedBuilder()
                        .WithAuthor(user)
                        .WithTitle($"Suggestion #{GetSNum(guild.Id)}")
                        .WithDescription(suggestion)
                        .WithOkColor(), n);
                
                IEmote[] reacts = { tup, tdown };
                if (em == null || em == "disabled")
                    foreach (var i in reacts) await t.AddReactionAsync(i);
                else
                    foreach (var ei in emotes) await t.AddReactionAsync(ei);
                await sugnum(guild, sugnum1 + 1);
                await Suggest(guild, sugnum1, t.Id, user.Id, suggestion);
            }
            else
            {
                CREmbed crEmbed = null;
                var sugnum1 = GetSNum(guild.Id);
                var replacer = new ReplacementBuilder()
                .WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", () => user.ToString())
                .WithOverride("%suggest.message%", () => suggestion)
                .WithOverride("%suggest.number%", () => sugnum1.ToString())
                .WithOverride("%suggest.user.name%", () => user.Username)
                .WithOverride("%suggest.user.avatar%", () => user.RealAvatarUrl(2048).ToString())
                .Build();
                var ebe = CREmbed.TryParse(GetSuggestionMessage(guild), out crEmbed);
                if (ebe is false)
                {
                    await channel.SendErrorAsync("The suggest message set is invalid, I have set it back to default to avoid further issues. Please set it again and try again. If you are having an issue please visit the suport server shown when you mention Mewdeko.");
                    await SetSuggestionMessage(guild, "-");
                    return;
                }
                replacer.Replace(crEmbed);
                var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
                if (crEmbed.PlainText != null && crEmbed.IsEmbedValid)
                {
                    var t = await chan.SendMessageAsync(crEmbed.PlainText.SanitizeMentions(true), embed: crEmbed.ToEmbed().Build());
                    IEmote[] reacts = { tup, tdown };
                    if (em == null || em == "disabled" || em == "-")
                        foreach (var i in reacts) await t.AddReactionAsync(i);
                    else
                        foreach (var ei in emotes) await t.AddReactionAsync(ei);
                    await sugnum(guild, sugnum1 + 1);
                    await Suggest(guild, sugnum1, t.Id, user.Id, suggestion);
                }
                if (crEmbed.PlainText is null)
                {
                    var t = await chan.SendMessageAsync(embed: crEmbed.ToEmbed().Build());
                    IEmote[] reacts = { tup, tdown };
                    if (em == null || em == "disabled" || em == "-")
                        foreach (var i in reacts) await t.AddReactionAsync(i);
                    else
                        foreach (var ei in emotes) await t.AddReactionAsync(ei);
                    await sugnum(guild, sugnum1 + 1);
                    await Suggest(guild, sugnum1, t.Id, user.Id, suggestion);
                }
                if (crEmbed.PlainText != null && !crEmbed.IsEmbedValid)
                {
                    var t = await chan.SendMessageAsync(crEmbed.PlainText.SanitizeMentions(true));
                    IEmote[] reacts = { tup, tdown };
                    if (em == null || em == "disabled" || em == "-")
                        foreach (var i in reacts) await t.AddReactionAsync(i);
                    else
                        foreach (var ei in emotes) await t.AddReactionAsync(ei);
                    await sugnum(guild, sugnum1 + 1);
                    await Suggest(guild, sugnum1, t.Id, user.Id, suggestion);
                }
                
            }
        }
        public async Task Suggest(IGuild guild, ulong SuggestID, ulong MessageID, ulong UserID, string suggestion)
        {
            var guildId = guild.Id;

            var suggest = new Suggestionse
            {
                GuildId = guildId,
                SuggestID = SuggestID,
                MessageID = MessageID,
                UserID = UserID,
                Suggestion = suggestion
            };
            using var uow = _db.GetDbContext();
            uow.Suggestions.Add(suggest);

            await uow.SaveChangesAsync();
        }
        public Suggestionse[] Suggestionse(ulong gid, ulong sid)
        {
            using var uow = _db.GetDbContext();
            return uow.Suggestions.ForId(gid, sid);
        }
    }
}