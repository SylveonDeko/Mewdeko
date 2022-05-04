using Discord;
using Discord.Net;
using Discord.WebSocket;
using LinqToDB;
using Mewdeko.Common;
using Mewdeko.Common.Replacements;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Serilog;

namespace Mewdeko.Modules.Suggestions.Services;

public class SuggestionsService : INService
{
    private readonly DbService _db;
    private readonly PermissionService _perms;
    public readonly DiscordSocketClient Client;
    public readonly AdministrationService Adminserv;
    private readonly Mewdeko _bot;
    private readonly List<ulong> _repostChecking;
    private readonly List<ulong> _spamCheck;

    public readonly CommandHandler CmdHandler;
    
    public SuggestionsService(
        DbService db,
        Mewdeko bot,
        CommandHandler cmd,
        DiscordSocketClient client,
        AdministrationService aserv,
        PermissionService permserv)
    {
        _perms = permserv;
        _repostChecking = new List<ulong>();
        _spamCheck = new List<ulong>();
        Adminserv = aserv;
        CmdHandler = cmd;
        Client = client;
        client.MessageReceived += MessageRecieved;
        client.ReactionAdded += UpdateCountOnReact;
        client.ReactionRemoved += UpdateCountOnRemoveReact;
        client.MessageReceived += RepostButton;
        _db = db;
        _bot = bot;
    }

    public enum SuggestState
    {
        Suggested = 0,
        Accepted = 1,
        Denied = 2,
        Considered = 3,
        Implemented = 4
    }

    private Task RepostButton(SocketMessage arg)
    {
        _ = Task.Run(async () =>
        {
            IEnumerable<IMessage> messages;
            if (arg.Channel is not ITextChannel channel)
                return;

            var buttonChannel = GetSuggestButtonChannel(channel.Guild);
            if (buttonChannel != channel.Id)
                return;

            if (GetSuggestButtonRepost(channel.Guild) == 0)
                return;
            if (_repostChecking.Contains(channel.Id))
                return;
            _repostChecking.Add(channel.Id);
            var buttonId = GetSuggestButtonMessageId(channel.Guild);
            if (GetSuggestButtonRepost(channel.Guild) is 0)
            {
                _repostChecking.Remove(channel.Id);
                return;
            }

            try
            {
                messages = await channel.GetMessagesAsync(arg, Direction.Before, GetSuggestButtonRepost(channel.Guild)).FlattenAsync();
            }
            catch (HttpException)
            {
                _repostChecking.Remove(channel.Id);
                return;
            }

            if (messages.Select(x => x.Id).Contains(buttonId))
            {
                _repostChecking.Remove(channel.Id);
                return;
            }

            if (buttonId != 0)
                try
                {
                    await channel.DeleteMessageAsync(buttonId);
                }
                catch (HttpException)
                {
                    Log.Error($"Button Repost will not work because of missing permissions in guild {channel.Guild}");
                    _repostChecking.Remove(channel.Id);
                    return;
                }

            var message = GetSuggestButtonMessage(channel.Guild);
            if (string.IsNullOrWhiteSpace(message) || message is "disabled" or "-")
            {
                var eb = new EmbedBuilder().WithOkColor().WithDescription("Press the button below to make a suggestion!");
                var toAdd = await channel.SendMessageAsync(embed: eb.Build(), components: GetSuggestButton(channel.Guild).Build());
                await SetSuggestionButtonId(channel.Guild, toAdd.Id);
                _repostChecking.Remove(channel.Id);
                return;
            }

            if (SmartEmbed.TryParse(GetSuggestButtonMessage(channel.Guild), out var embed, out var plainText))
            {
                try
                {
                    var toadd = await channel.SendMessageAsync(plainText, embed: embed?.Build(), components: GetSuggestButton(channel.Guild).Build());
                    await SetSuggestionButtonId(channel.Guild, toadd.Id);
                    _repostChecking.Remove(channel.Id);
                }
                catch (NullReferenceException)
                {
                    _repostChecking.Remove(channel.Id);
                }
            }
            else
            {
                try
                {
                    var toadd = await channel.SendMessageAsync(GetSuggestButtonMessage(channel.Guild), components: GetSuggestButton(channel.Guild).Build());
                    await SetSuggestionButtonId(channel.Guild, toadd.Id);
                    _repostChecking.Remove(channel.Id);
                }
                catch (NullReferenceException)
                {
                    _repostChecking.Remove(channel.Id);
                }
            }

            _repostChecking.Remove(channel.Id);
        });
        return Task.CompletedTask;
    }


    private Task UpdateCountOnReact(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3)
    {
        _ = Task.Run(async () =>
        {
            var message = await arg1.GetOrDownloadAsync();
            if (message is null)
                return;

            if (await arg2.GetOrDownloadAsync() is not ITextChannel channel)
                return;
            if (channel.Id != GetSuggestionChannel(channel.Guild.Id))
                return;
            await using var uow = _db.GetDbContext();
            var maybeSuggest = uow.Suggestions.FirstOrDefault(x => x.GuildId == channel.GuildId && x.MessageId == message.Id);
            if (maybeSuggest is null)
                return;
            var tup = new Emoji("\uD83D\uDC4D");
            var tdown = new Emoji("\uD83D\uDC4E");
            var toSplit = GetEmotes(channel.GuildId);
            if (toSplit is "disabled" or "-" or null)
            {
                if (Equals(arg3.Emote, tup))
                {
                    maybeSuggest.EmoteCount1 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
                    uow.Suggestions.Update(maybeSuggest);
                    await uow.SaveChangesAsync();
                }
                else if (Equals(arg3.Emote, tdown))
                {
                    maybeSuggest.EmoteCount2 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
                    uow.Suggestions.Update(maybeSuggest);
                    await uow.SaveChangesAsync();
                }
                else
                    return;
            }

            var emotes = toSplit.Split(",");
            if (Equals(arg3.Emote, emotes[0].ToIEmote()))
                maybeSuggest.EmoteCount1 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
            else if (Equals(arg3.Emote, emotes[1].ToIEmote()))
                maybeSuggest.EmoteCount2 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
            else if (Equals(arg3.Emote, emotes[2].ToIEmote()))
                maybeSuggest.EmoteCount3 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
            else if (Equals(arg3.Emote, emotes[3].ToIEmote()))
                maybeSuggest.EmoteCount4 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
            else if (Equals(arg3.Emote, emotes[4].ToIEmote()))
                maybeSuggest.EmoteCount5 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
            else
                return;

            uow.Suggestions.Update(maybeSuggest);
            await uow.SaveChangesAsync();
        });
        return Task.CompletedTask;
    }

    private Task UpdateCountOnRemoveReact(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3)
    {
        _ = Task.Run(async () =>
        {
            var message = await arg1.GetOrDownloadAsync();
            if (message is null)
                return;

            if (await arg2.GetOrDownloadAsync() is not ITextChannel channel)
                return;
            if (channel.Id != GetSuggestionChannel(channel.Guild.Id))
                return;
            await using var uow = _db.GetDbContext();
            var maybeSuggest = uow.Suggestions.FirstOrDefault(x => x.GuildId == channel.GuildId && x.MessageId == message.Id);
            if (maybeSuggest is null)
                return;
            var tup = new Emoji("\uD83D\uDC4D");
            var tdown = new Emoji("\uD83D\uDC4E");
            var toSplit = GetEmotes(channel.GuildId);
            if (toSplit is "disabled" or "-" or null)
            {
                if (Equals(arg3.Emote, tup))
                {
                    maybeSuggest.EmoteCount1 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
                    uow.Suggestions.Update(maybeSuggest);
                    await uow.SaveChangesAsync();
                }
                else if (Equals(arg3.Emote, tdown))
                {
                    maybeSuggest.EmoteCount2 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
                    uow.Suggestions.Update(maybeSuggest);
                    await uow.SaveChangesAsync();
                }
                else
                    return;
            }

            var emotes = toSplit.Split(",");
            if (Equals(arg3.Emote, emotes[0].ToIEmote()))
                maybeSuggest.EmoteCount1 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
            else if (Equals(arg3.Emote, emotes[1].ToIEmote()))
                maybeSuggest.EmoteCount2 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
            else if (Equals(arg3.Emote, emotes[2].ToIEmote()))
                maybeSuggest.EmoteCount3 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
            else if (Equals(arg3.Emote, emotes[3].ToIEmote()))
                maybeSuggest.EmoteCount4 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
            else if (Equals(arg3.Emote, emotes[4].ToIEmote()))
                maybeSuggest.EmoteCount5 = (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync()).Count(x => !x.IsBot);
            else
                return;

            uow.Suggestions.Update(maybeSuggest);
            await uow.SaveChangesAsync();
        });
        return Task.CompletedTask;
    }

    private Task MessageRecieved(SocketMessage msg)
    {
        _ = Task.Run(async () =>
        {
            if (msg.Channel is not ITextChannel chan)
                return;
            if (_spamCheck.Contains(chan.Id))
                return;
            _spamCheck.Add(chan.Id);
            var guild = chan?.Guild;
            var prefix = CmdHandler.GetPrefix(guild);
            if (guild != null && msg.Author.IsBot == false && !msg.Content.StartsWith(prefix))
            {
                if (chan.Id != GetSuggestionChannel(guild.Id))
                {
                    _spamCheck.Remove(chan.Id);
                    return;
                }

                var guser = msg.Author as IGuildUser;
                var pc = _perms.GetCacheFor(guild.Id);
                var test = pc.Permissions.CheckPermissions(msg as IUserMessage, "suggest", "Suggestions".ToLowerInvariant(), out _);
                if (!test)
                {
                    _spamCheck.Remove(chan.Id);
                    return;
                }

                if (guser.RoleIds.Contains(Adminserv.GetStaffRole(guser.Guild.Id)))
                {
                    _spamCheck.Remove(chan.Id);
                    return;
                }

                if (msg.Content.Length > GetMaxLength(guild.Id))
                {
                    try
                    {
                        await msg.DeleteAsync();
                    }
                    catch
                    {
                        // ignore
                    }

                    try
                    {
                        await guser.SendErrorAsync($"Cannot send this suggestion as its over the max length `({GetMaxLength(guild.Id)})` of this server!");
                        _spamCheck.Remove(chan.Id);
                    }
                    catch
                    {
                        // ignore
                    }

                    return;
                }

                if (msg.Content.Length < GetMinLength(guild.Id))
                {
                    try
                    {
                        await msg.DeleteAsync();
                    }
                    catch
                    {
                        // ignore
                    }

                    try
                    {
                        await guser.SendErrorAsync($"Cannot send this suggestion as its under the minimum length `({GetMaxLength(guild.Id)})` of this server!");
                        _spamCheck.Remove(chan.Id);
                    }
                    catch
                    {
                        // ignore
                    }

                    return;
                }

                await SendSuggestion(chan.Guild, msg.Author as IGuildUser, Client, msg.Content, msg.Channel as ITextChannel);
                _spamCheck.Remove(chan.Id);
                try
                {
                    await msg.DeleteAsync();
                }
                catch
                {
                    //ignored
                }
            }
        });
        return Task.CompletedTask;
    }

    public ulong GetSNum(ulong? id) => _bot.GetGuildConfig(id.Value).sugnum;
    public int GetMaxLength(ulong? id) => _bot.GetGuildConfig(id.Value).MaxSuggestLength;
    public int GetMinLength(ulong? id) => _bot.GetGuildConfig(id.Value).MinSuggestLength;

    public string GetEmotes(ulong? id) => _bot.GetGuildConfig(id.Value).SuggestEmotes;

    public async Task SetButtonType(IGuild guild, int buttonId, int color)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        switch (buttonId)
        {
            case 1:
                gc.Emote1Style = color;
                break;
            case 2:
                gc.Emote2Style = color;
                break;
            case 3:
                gc.Emote3Style = color;
                break;
            case 4:
                gc.Emote4Style = color;
                break;
            case 5:
                gc.Emote5Style = color;
                break;
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public Task<(ulong, ulong)> GetRepostedMessageAndChannel(SuggestionsModel suggestions, IGuild guild)
    {
        (ulong, ulong) toreturn = suggestions.CurrentState switch
        {
            1 => (suggestions.StateChangeMessageId, GetAcceptChannel(guild)),
            2 => (suggestions.StateChangeMessageId, GetDenyChannel(guild)),
            3 => (suggestions.StateChangeMessageId, GetConsiderChannel(guild)),
            4 => (suggestions.StateChangeMessageId, GetImplementChannel(guild)),
            _ => (0, 0)
        };
        return Task.FromResult(toreturn);
    }

    public async Task SetSuggestionEmotes(IGuild guild, string parsedEmotes)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestEmotes = parsedEmotes;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetSuggestButtonColor(IGuild guild, int colorNum)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonColor = colorNum;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetSuggestionButtonId(IGuild guild, ulong messageId)
    {
        try
        {
            await using var uow = _db.GetDbContext();
            var gc = uow.ForGuildId(guild.Id, set => set);
            gc.SuggestButtonMessageId = messageId;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            _bot.UpdateGuildConfig(guild.Id, gc);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task SetSuggestionChannelId(IGuild guild, ulong channel)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.sugchan = channel;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetMinLength(IGuild guild, int minLength)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.MinSuggestLength = minLength;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetMaxLength(IGuild guild, int maxLength)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.MaxSuggestLength = maxLength;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public ComponentBuilder GetSuggestButton(IGuild guild)
    {
        string buttonLabel;
        IEmote buttonEmote;
        var builder = new ComponentBuilder();
        if (string.IsNullOrWhiteSpace(GetSuggestButtonName(guild)) || GetSuggestButtonName(guild) is "disabled" or "-")
            buttonLabel = "Suggest Here!";
        else
            buttonLabel = GetSuggestButtonName(guild);
        if (string.IsNullOrWhiteSpace(GetSuggestButtonEmote(guild)) || GetSuggestButtonEmote(guild) is "disabled" or "-")
            buttonEmote = null;
        else
            buttonEmote = GetSuggestButtonEmote(guild).ToIEmote();
        builder.WithButton(buttonLabel, "suggestbutton", GetSuggestButtonColor(guild), emote: buttonEmote);
        return builder;
    }

    public async Task SuggestReset(IGuild guild)
    {
        await using var uow = _db.GetDbContext();
        await uow.Suggestions.DeleteAsync();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.sugnum = 1;
        await uow.SaveChangesAsync();
    }
    public async Task UpdateSuggestionButtonMessage(IGuild guild, string code, bool bypasschannelcheck = false)
    {
        var toGet = GetSuggestButtonChannel(guild);
        if (toGet is 0 && !bypasschannelcheck)
            return;
        var channel = await guild.GetTextChannelAsync(toGet);
        if (channel is null)
            return;
        var messageId = GetSuggestButtonMessageId(guild);
        try
        {
            if (messageId is 0)
                messageId = 999;
            var message = await channel.GetMessageAsync(messageId);
            if (message is null)
            {
                if (SmartEmbed.TryParse(code, out var embed, out var plainText))
                {
                    var toadd = await channel.SendMessageAsync(plainText, embed: embed?.Build(), components: GetSuggestButton(channel.Guild).Build());
                    await SetSuggestionButtonId(channel.Guild, toadd.Id);
                    return;
                }
                if (code is "-")
                {
                    var eb = new EmbedBuilder().WithOkColor().WithDescription("Press the button below to make a suggestion!");
                    var toadd = await channel.SendMessageAsync(plainText, embed: eb.Build(), components: GetSuggestButton(channel.Guild).Build());
                    await SetSuggestionButtonId(channel.Guild, toadd.Id);
                    return;
                }
                else
                {
                    var toadd = await channel.SendMessageAsync(code, components: GetSuggestButton(channel.Guild).Build());
                    await SetSuggestionButtonId(channel.Guild, toadd.Id);
                    return;
                }
            }

            if (code is "-")
            {
                var eb = new EmbedBuilder().WithOkColor().WithDescription("Press the button below to make a suggestion!");
                try
                {
                    await ((IUserMessage)message).ModifyAsync(x =>
                    {
                        x.Embed = eb.Build();
                        x.Content = null;
                        x.Components = GetSuggestButton(channel.Guild).Build();
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            else
            {
                if (SmartEmbed.TryParse(code, out var embed, out var plainText))
                {
                    await ((IUserMessage)message).ModifyAsync(x =>
                    {
                        x.Embed = embed?.Build();
                        x.Content = plainText;
                        x.Components = GetSuggestButton(channel.Guild).Build();
                    });
                }
                else
                {
                    await ((IUserMessage)message).ModifyAsync(x =>
                    {
                        x.Content = code;
                        x.Embed = null;
                        x.Components = GetSuggestButton(channel.Guild).Build();
                    });
                }
            }
        }
        catch (HttpException)
        {
            // ignored
        }
    }

    public async Task SetSuggestButtonMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetSuggestButtonLabel(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonName = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetSuggestionMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetAcceptMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.AcceptMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetDenyMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.DenyMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetImplementMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.ImplementMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task UpdateSuggestState(SuggestionsModel suggestionsModel, int state, ulong stateChangeId)
    {
        await using var uow = _db.GetDbContext();
        suggestionsModel.CurrentState = state;
        suggestionsModel.StateChangeUser = stateChangeId;
        suggestionsModel.StateChangeCount += 1;
        uow.Suggestions.Update(suggestionsModel);
        await uow.SaveChangesAsync();
    }

    public async Task UpdateStateMessageId(SuggestionsModel suggestionsModel, ulong messageStateId)
    {
        await using var uow = _db.GetDbContext();
        suggestionsModel.StateChangeMessageId = messageStateId;
        uow.Suggestions.Update(suggestionsModel);
        await uow.SaveChangesAsync();
    }

    public async Task SetSuggestThreadsType(IGuild guild, int num)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestionThreadType = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetConsiderMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.ConsiderMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task Sugnum(IGuild guild, ulong num)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.sugnum = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetArchiveOnDeny(IGuild guild, bool value)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.ArchiveOnDeny = value;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }
    public async Task SetArchiveOnAccept(IGuild guild, bool value)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.ArchiveOnAccept = value;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }
    public async Task SetArchiveOnConsider(IGuild guild, bool value)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.ArchiveOnConsider = value;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }
    public async Task SetArchiveOnImplement(IGuild guild, bool value)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.ArchiveOnImplement = value;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }
    public async Task SetEmoteMode(IGuild guild, int mode)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.EmoteMode = mode;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetAcceptChannel(IGuild guild, ulong channelId)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.AcceptChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetDenyChannel(IGuild guild, ulong channelId)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.DenyChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetConsiderChannel(IGuild guild, ulong channelId)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.ConsiderChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetImplementChannel(IGuild guild, ulong channelId)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.ImplementChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetSuggestButtonChannel(IGuild guild, ulong channelId)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetSuggestButtonEmote(IGuild guild, string emote)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonEmote = emote;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetSuggestButtonRepostThreshold(IGuild guild, int repostThreshold)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonRepostThreshold = repostThreshold;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task UpdateEmoteCount(ulong messageId, int emoteNumber, bool negative = false)
    {
        await using var uow = _db.GetDbContext();
        var suggest = uow.Suggestions.FirstOrDefault(x => x.MessageId == messageId);
        uow.Suggestions.Remove(suggest);
        await uow.SaveChangesAsync();
        switch (negative)
        {
            case false:
                switch (emoteNumber)
                {
                    case 1:
                        ++suggest.EmoteCount1;
                        break;
                    case 2:
                        ++suggest.EmoteCount2;
                        break;
                    case 3:
                        ++suggest.EmoteCount3;
                        break;
                    case 4:
                        ++suggest.EmoteCount4;
                        break;
                    case 5:
                        ++suggest.EmoteCount5;
                        break;
                }

                break;
            default:
                switch (emoteNumber)
                {
                    case 1:
                        --suggest.EmoteCount1;
                        break;
                    case 2:
                        --suggest.EmoteCount2;
                        break;
                    case 3:
                        --suggest.EmoteCount3;
                        break;
                    case 4:
                        --suggest.EmoteCount4;
                        break;
                    case 5:
                        --suggest.EmoteCount5;
                        break;
                }

                break;
        }

        uow.Suggestions.Add(suggest);
        await uow.SaveChangesAsync();
    }

    public async Task<int> GetCurrentCount(IGuild guild, ulong messageId, int emoteNumber)
    {
        int count;
        await using var uow = _db.GetDbContext();
        var toupdate = uow.Suggestions.FirstOrDefault(x => x.MessageId == messageId);
        count = emoteNumber switch
        {
            1 => toupdate.EmoteCount1,
            2 => toupdate.EmoteCount2,
            3 => toupdate.EmoteCount3,
            4 => toupdate.EmoteCount4,
            5 => toupdate.EmoteCount5,
            _ => 0
        };
        return count;
    }

    public IEmote GetSuggestMote(IGuild guild, int num)
    {
        var tup = new Emoji("\uD83D\uDC4D");
        var tdown = new Emoji("\uD83D\uDC4E");
        var emotes = _bot.GetGuildConfig(guild.Id).SuggestEmotes;
        if (emotes is null or "disabled")
        {
            return num == 1 ? tup : tdown;
        }

        return emotes.Split(",")[num - 1].ToIEmote();
    }

    public ulong GetSuggestionChannel(ulong? id) => _bot.GetGuildConfig(id.Value).sugchan;

    public string GetSuggestionMessage(IGuild guild) => _bot.GetGuildConfig(guild.Id).SuggestMessage;

    public string GetAcceptMessage(IGuild guild) => _bot.GetGuildConfig(guild.Id).AcceptMessage;

    public string GetDenyMessage(IGuild guild) => _bot.GetGuildConfig(guild.Id).DenyMessage;

    public string GetImplementMessage(IGuild guild) => _bot.GetGuildConfig(guild.Id).ImplementMessage;

    public string GetConsiderMessage(IGuild guild) => _bot.GetGuildConfig(guild.Id).ConsiderMessage;


    public int GetThreadType(IGuild guild) => _bot.GetGuildConfig(guild.Id).SuggestionThreadType;

    public int GetEmoteMode(IGuild guild) => _bot.GetGuildConfig(guild.Id).EmoteMode;

    public ulong GetConsiderChannel(IGuild guild) => _bot.GetGuildConfig(guild.Id).ConsiderChannel;

    public ulong GetAcceptChannel(IGuild guild) => _bot.GetGuildConfig(guild.Id).AcceptChannel;

    public ulong GetImplementChannel(IGuild guild) => _bot.GetGuildConfig(guild.Id).ImplementChannel;

    public ulong GetDenyChannel(IGuild guild) => _bot.GetGuildConfig(guild.Id).DenyChannel;

    public bool GetArchiveOnDeny(IGuild guild) => _bot.GetGuildConfig(guild.Id).ArchiveOnDeny;

    public bool GetArchiveOnAccept(IGuild guild) => _bot.GetGuildConfig(guild.Id).ArchiveOnAccept;

    public bool GetArchiveOnConsider(IGuild guild) => _bot.GetGuildConfig(guild.Id).ArchiveOnConsider;

    public bool GetArchiveOnImplement(IGuild guild) => _bot.GetGuildConfig(guild.Id).ArchiveOnImplement;

    public string GetSuggestButtonName(IGuild guild) => _bot.GetGuildConfig(guild.Id).SuggestButtonName;

    public ulong GetSuggestButtonChannel(IGuild guild) => _bot.GetGuildConfig(guild.Id).SuggestButtonChannel;

    public string GetSuggestButtonEmote(IGuild guild) => _bot.GetGuildConfig(guild.Id).SuggestButtonEmote;

    public string GetSuggestButtonMessage(IGuild guild) => _bot.GetGuildConfig(guild.Id).SuggestButtonMessage;

    public int GetSuggestButtonRepost(IGuild guild) => _bot.GetGuildConfig(guild.Id).SuggestButtonRepostThreshold;

    public ulong GetSuggestButtonMessageId(IGuild guild) => _bot.GetGuildConfig(guild.Id).SuggestButtonMessageId;

    public ButtonStyle GetSuggestButtonColor(IGuild guild) => (ButtonStyle)_bot.GetGuildConfig(guild.Id).SuggestButtonColor;

    public ButtonStyle GetButtonStyle(IGuild guild, int id) =>
        id switch
        {
            1 => (ButtonStyle)_bot.GetGuildConfig(guild.Id).Emote1Style,
            2 => (ButtonStyle)_bot.GetGuildConfig(guild.Id).Emote2Style,
            3 => (ButtonStyle)_bot.GetGuildConfig(guild.Id).Emote3Style,
            4 => (ButtonStyle)_bot.GetGuildConfig(guild.Id).Emote4Style,
            5 => (ButtonStyle)_bot.GetGuildConfig(guild.Id).Emote5Style,
            _ => ButtonStyle.Secondary
        };


    public async Task SendDenyEmbed(
        IGuild guild,
        DiscordSocketClient client,
        IUser user,
        ulong suggestion,
        ITextChannel channel,
        string? reason = null,
        IDiscordInteraction? interaction = null)
    {
        string rs;
        rs = reason ?? "none";
        var suggest = Suggestions(guild.Id, suggestion).FirstOrDefault();
        if (suggest is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.");
                return;
            }

            await interaction.SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.");
            return;
        }

        var use = await guild.GetUserAsync(suggest.UserId);
        EmbedBuilder eb;
        if (GetDenyMessage(guild) is "-" or "" or null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Denied").WithDescription(suggest.Suggestion).WithOkColor().AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).GetMessageAsync(suggest.MessageId);
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Denied").WithDescription(desc.Embeds.FirstOrDefault()?.Description).WithOkColor()
                                       .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction.SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!");
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!");
                return;
            }

            if (GetArchiveOnDeny(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(GetThreadByMessage(suggest.MessageId));
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (await chan.GetMessageAsync(suggest.MessageId) is IUserMessage message)
            {
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                });
                try
                {
                    await message.RemoveAllReactionsAsync();
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                var msg = await chan.SendMessageAsync(embed: eb.Build());
                suggest.MessageId = msg.Id;
                await using var uow = _db.GetDbContext();
                uow.Update(suggest);
                await uow.SaveChangesAsync();
            }

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Denied");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Denied By", user);
                emb.WithErrorColor();
                await (await guild.GetUserAsync(suggest.UserId)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as denied and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as denied and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as denied but the user had their DMs off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as denied but the user had DMs off.");
            }

            await UpdateSuggestState(suggest, (int)SuggestState.Denied, user.Id);
            if (GetDenyChannel(guild) is not 0)
            {
                var denyChannel = await guild.GetTextChannelAsync(GetDenyChannel(guild));
                if (denyChannel is null)
                    return;
                if (!(await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(denyChannel).EmbedLinks)
                    return;
                if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                {
                    var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                    var toCheck = await guild.GetTextChannelAsync(messageChannel);
                    if (toCheck is not null)
                    {
                        var messageCheck = await toCheck.GetMessageAsync(messageId);
                        if (messageCheck is not null)
                            try
                            {
                                await messageCheck.DeleteAsync();
                            }
                            catch
                            {
                                // ignored
                            }
                    }
                }

                var toSet = await denyChannel.SendMessageAsync(embed: eb.Build());
                await UpdateStateMessageId(suggest, toSet.Id);
            }
        }
        else
        {
            string sug;
            if (suggest.Suggestion == null)
                sug = (await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).GetMessageAsync(suggest.MessageId)).Embeds.FirstOrDefault()?.Description;
            else
                sug = suggest.Suggestion;
            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction.SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!");
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!");
                return;
            }

            var message = await chan.GetMessageAsync(suggest.MessageId) as IUserMessage;
            var suguse = await guild.GetUserAsync(suggest.UserId);
            var replacer = new ReplacementBuilder().WithServer(client, guild as SocketGuild).WithOverride("%suggest.user%", () => suguse.ToString())
                                                   .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                                                   .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                                                   .WithOverride("%suggest.number%", () => suggest.SuggestionId.ToString())
                                                   .WithOverride("%suggest.user.name%", () => suguse.Username)
                                                   .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                                                   .WithOverride("%suggest.mod.user%", () => user.ToString())
                                                   .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                                                   .WithOverride("%suggest.mod.name%", () => user.Username).WithOverride("%suggest.mod.message%", () => rs)
                                                   .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                                                   .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                                                   .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                                                   .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                                                   .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                                                   .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString()).Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(GetDenyMessage(guild)), out var embed, out var plainText);
            if (GetArchiveOnDeny(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(GetThreadByMessage(suggest.MessageId));
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (ebe is false)
            {
                if (message is not null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Embed = null;
                        x.Content = replacer.Replace(GetDenyMessage(guild));
                    });
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(replacer.Replace(GetDenyMessage(guild)));
                    suggest.MessageId = toReplace.Id;
                    await using var uow = _db.GetDbContext();
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync();
                }

                await UpdateSuggestState(suggest, (int)SuggestState.Denied, user.Id);
                if (GetDenyChannel(guild) is not 0)
                {
                    var denyChannel = await guild.GetTextChannelAsync(GetDenyChannel(guild));
                    if (denyChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(denyChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId);
                                    if (messageCheck is not null)
                                        try
                                        {
                                            await messageCheck.DeleteAsync();
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                }
                            }

                            var toSet = await denyChannel.SendMessageAsync(replacer.Replace(GetDenyMessage(guild)));
                            await UpdateStateMessageId(suggest, toSet.Id);
                        }
                    }
                }
            }
            else
            {
                if (message is not null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = plainText;
                        x.Embed = embed?.Build();
                    });
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(plainText, embed: embed?.Build());
                    await using var uow = _db.GetDbContext();
                    suggest.MessageId = toReplace.Id;
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync();
                }

                if (GetDenyChannel(guild) is not 0)
                {
                    var denyChannel = await guild.GetTextChannelAsync(GetDenyChannel(guild));
                    if (denyChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(denyChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId);
                                    if (messageCheck is not null)
                                        try
                                        {
                                            await messageCheck.DeleteAsync();
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                }
                            }

                            var toSet = await denyChannel.SendMessageAsync(plainText, embed: embed?.Build());
                            await UpdateStateMessageId(suggest, toSet.Id);
                        }
                    }
                }
            }

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Denied");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Denied By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserId)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as denied and the user has been dmed the denial!");
                else
                    await interaction.SendConfirmAsync("Suggestion set as denied and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as denied but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as denied but the user had DMs off.");
            }
        }
    }

    public async Task SendConsiderEmbed(
        IGuild guild,
        DiscordSocketClient client,
        IUser user,
        ulong suggestion,
        ITextChannel channel,
        string? reason = null,
        IDiscordInteraction? interaction = null)
    {
        string rs;
        if (reason == null)
            rs = "none";
        else
            rs = reason;
        var suggest = Suggestions(guild.Id, suggestion).FirstOrDefault();
        if (suggest.Suggestion is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.");
                return;
            }

            await interaction.SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.");
            return;
        }

        var use = await guild.GetUserAsync(suggest.UserId);
        EmbedBuilder eb;
        if (GetConsiderMessage(guild) is "-" or "" or null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Considering").WithDescription(suggest.Suggestion).WithOkColor().AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).GetMessageAsync(suggest.MessageId);
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Considering").WithDescription(desc.Embeds.FirstOrDefault().Description).WithOkColor()
                                       .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction.SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!");
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!");
                return;
            }

            if (GetArchiveOnConsider(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(GetThreadByMessage(suggest.MessageId));
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (await chan.GetMessageAsync(suggest.MessageId) is IUserMessage message)
            {
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                });
                try
                {
                    await message.RemoveAllReactionsAsync();
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                var msg = await chan.SendMessageAsync(embed: eb.Build());
                suggest.MessageId = msg.Id;
                await using var uow = _db.GetDbContext();
                uow.Update(suggest);
                await uow.SaveChangesAsync();
            }

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Considering");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Denied By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserId)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered but the user had DMs off.");
            }

            await UpdateSuggestState(suggest, (int)SuggestState.Considered, user.Id);
            if (GetConsiderChannel(guild) is not 0)
            {
                var considerChannel = await guild.GetTextChannelAsync(GetConsiderChannel(guild));
                if (considerChannel is null)
                    return;
                if (!(await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(considerChannel).EmbedLinks)
                    return;
                if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                {
                    var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                    var toCheck = await guild.GetTextChannelAsync(messageChannel);
                    if (toCheck is not null)
                    {
                        var messageCheck = await toCheck.GetMessageAsync(messageId);
                        if (messageCheck is not null)
                            try
                            {
                                await messageCheck.DeleteAsync();
                            }
                            catch
                            {
                                // ignored
                            }
                    }
                }

                var toSet = await considerChannel.SendMessageAsync(embed: eb.Build());
                await UpdateStateMessageId(suggest, toSet.Id);
            }
        }
        else
        {
            string sug;
            if (suggest.Suggestion == null)
                sug = (await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).GetMessageAsync(suggest.MessageId)).Embeds.FirstOrDefault()!.Description;
            else
                sug = suggest.Suggestion;
            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction.SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!");
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!");
                return;
            }

            var message = await chan.GetMessageAsync(suggest.MessageId) as IUserMessage;
            var suguse = await guild.GetUserAsync(suggest.UserId);
            var replacer = new ReplacementBuilder().WithServer(client, guild as SocketGuild).WithOverride("%suggest.user%", () => suguse.ToString())
                                                   .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                                                   .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                                                   .WithOverride("%suggest.number%", () => suggest.SuggestionId.ToString())
                                                   .WithOverride("%suggest.user.name%", () => suguse.Username)
                                                   .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString()).WithOverride("%suggest.mod.user%", user.ToString)
                                                   .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                                                   .WithOverride("%suggest.mod.name%", () => user.Username).WithOverride("%suggest.mod.message%", () => rs)
                                                   .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                                                   .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                                                   .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                                                   .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                                                   .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                                                   .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString()).Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(GetConsiderMessage(guild)), out var embed, out var plainText);
            if (GetArchiveOnConsider(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(GetThreadByMessage(suggest.MessageId));
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (ebe is false)
            {
                if (message is not null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Embed = null;
                        x.Content = replacer.Replace(GetConsiderMessage(guild));
                    });
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(replacer.Replace(GetConsiderMessage(guild)));
                    suggest.MessageId = toReplace.Id;
                    await using var uow = _db.GetDbContext();
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync();
                }

                await UpdateSuggestState(suggest, (int)SuggestState.Considered, user.Id);
                if (GetConsiderChannel(guild) is not 0)
                {
                    var considerChannel = await guild.GetTextChannelAsync(GetConsiderChannel(guild));
                    if (considerChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(considerChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId);
                                    if (messageCheck is not null)
                                        try
                                        {
                                            await messageCheck.DeleteAsync();
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                }
                            }

                            var toSet = await considerChannel.SendMessageAsync(replacer.Replace(GetConsiderMessage(guild)));
                            await UpdateStateMessageId(suggest, toSet.Id);
                        }
                    }
                }
            }
            else
            {
                if (message is not null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = plainText;
                        x.Embed = embed?.Build();
                    });
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(plainText, embed: embed?.Build());
                    await using var uow = _db.GetDbContext();
                    suggest.MessageId = toReplace.Id;
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync();
                }

                if (GetConsiderChannel(guild) is not 0)
                {
                    var considerChannel = await guild.GetTextChannelAsync(GetConsiderChannel(guild));
                    if (considerChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(considerChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId);
                                    if (messageCheck is not null)
                                        try
                                        {
                                            await messageCheck.DeleteAsync();
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                }
                            }

                            var toSet = await considerChannel.SendMessageAsync(plainText, embed: embed?.Build());
                            await UpdateStateMessageId(suggest, toSet.Id);
                        }
                    }
                }
            }

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Considering");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Considered by", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserId)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered but the user had DMs off.");
            }
        }
    }

    public async Task SendImplementEmbed(
        IGuild guild,
        DiscordSocketClient client,
        IUser user,
        ulong suggestion,
        ITextChannel channel,
        string? reason = null,
        IDiscordInteraction? interaction = null)
    {
        string rs;
        if (reason == null)
            rs = "none";
        else
            rs = reason;
        var suggest = Suggestions(guild.Id, suggestion).FirstOrDefault();
        if (suggest.Suggestion is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.");
                return;
            }

            await interaction.SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.");
            return;
        }

        var use = await guild.GetUserAsync(suggest.UserId);
        EmbedBuilder eb;
        if (GetImplementMessage(guild) is "-" or "" or null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Implemented").WithDescription(suggest.Suggestion).WithOkColor().AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).GetMessageAsync(suggest.MessageId);
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Implemented").WithDescription(desc.Embeds.FirstOrDefault().Description).WithOkColor()
                                       .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction.SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!");
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!");
                return;
            }

            if (GetArchiveOnImplement(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(GetThreadByMessage(suggest.MessageId));
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (await chan.GetMessageAsync(suggest.MessageId) is IUserMessage message)
            {
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                });
                try
                {
                    await message.RemoveAllReactionsAsync();
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                var msg = await chan.SendMessageAsync(embed: eb.Build());
                suggest.MessageId = msg.Id;
                await using var uow = _db.GetDbContext();
                uow.Update(suggest);
                await uow.SaveChangesAsync();
            }

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Implemented");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Implemented By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserId)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented but the user had DMs off.");
            }

            await UpdateSuggestState(suggest, (int)SuggestState.Implemented, user.Id);
            if (GetImplementChannel(guild) is not 0)
            {
                var implementChannel = await guild.GetTextChannelAsync(GetImplementChannel(guild));
                if (implementChannel is null)
                    return;
                if (!(await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(implementChannel).EmbedLinks)
                    return;
                if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                {
                    var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                    var toCheck = await guild.GetTextChannelAsync(messageChannel);
                    if (toCheck is not null)
                    {
                        var messageCheck = await toCheck.GetMessageAsync(messageId);
                        if (messageCheck is not null)
                            try
                            {
                                await messageCheck.DeleteAsync();
                            }
                            catch
                            {
                                // ignored
                            }
                    }
                }

                var toSet = await implementChannel.SendMessageAsync(embed: eb.Build());
                await UpdateStateMessageId(suggest, toSet.Id);
            }
        }
        else
        {
            string sug;
            if (suggest.Suggestion == null)
                sug = (await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).GetMessageAsync(suggest.MessageId)).Embeds.FirstOrDefault()?.Description;
            else
                sug = suggest.Suggestion;
            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction.SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!");
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!");
                return;
            }

            var message = await chan.GetMessageAsync(suggest.MessageId) as IUserMessage;
            GetSNum(guild.Id);
            var suguse = await guild.GetUserAsync(suggest.UserId);
            var replacer = new ReplacementBuilder().WithServer(client, guild as SocketGuild).WithOverride("%suggest.user%", () => suguse.ToString())
                                                   .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                                                   .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                                                   .WithOverride("%suggest.number%", () => suggest.SuggestionId.ToString())
                                                   .WithOverride("%suggest.user.name%", () => suguse.Username)
                                                   .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                                                   .WithOverride("%suggest.mod.user%", () => user.ToString())
                                                   .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                                                   .WithOverride("%suggest.mod.name%", () => user.Username).WithOverride("%suggest.mod.message%", () => rs)
                                                   .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                                                   .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                                                   .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                                                   .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                                                   .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                                                   .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString()).Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(GetImplementMessage(guild)), out var embed, out var plainText);
            if (GetArchiveOnImplement(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(GetThreadByMessage(suggest.MessageId));
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (ebe is false)
            {
                if (message is not null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Embed = null;
                        x.Content = replacer.Replace(GetImplementMessage(guild));
                    });
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(replacer.Replace(GetImplementMessage(guild)));
                    suggest.MessageId = toReplace.Id;
                    await using var uow = _db.GetDbContext();
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync();
                }

                await UpdateSuggestState(suggest, (int)SuggestState.Implemented, user.Id);
                if (GetImplementChannel(guild) is not 0)
                {
                    var implementChannel = await guild.GetTextChannelAsync(GetImplementChannel(guild));
                    if (implementChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(implementChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId);
                                    if (messageCheck is not null)
                                        try
                                        {
                                            await messageCheck.DeleteAsync();
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                }
                            }

                            var toSet = await implementChannel.SendMessageAsync(replacer.Replace(GetImplementMessage(guild)));
                            await UpdateStateMessageId(suggest, toSet.Id);
                        }
                    }
                }
            }
            else
            {
                if (message is not null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = plainText;
                        x.Embed = embed?.Build();
                    });
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(plainText, embed: embed?.Build());
                    await using var uow = _db.GetDbContext();
                    suggest.MessageId = toReplace.Id;
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync();
                }

                if (GetImplementChannel(guild) is not 0)
                {
                    var implementChannel = await guild.GetTextChannelAsync(GetImplementChannel(guild));
                    if (implementChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(implementChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId);
                                    if (messageCheck is not null)
                                        try
                                        {
                                            await messageCheck.DeleteAsync();
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                }
                            }

                            var toSet = await implementChannel.SendMessageAsync(plainText, embed: embed?.Build());
                            await UpdateStateMessageId(suggest, toSet.Id);
                        }
                    }
                }
            }

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Implemented");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Implemented By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserId)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented but the user had DMs off.");
            }
        }
    }

    public async Task SendAcceptEmbed(
        IGuild guild,
        DiscordSocketClient client,
        IUser user,
        ulong suggestion,
        ITextChannel channel,
        string? reason = null,
        IDiscordInteraction? interaction = null)
    {
        var rs = reason ?? "none";
        var suggest = Suggestions(guild.Id, suggestion).FirstOrDefault();
        if (suggest.Suggestion is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.");
                return;
            }

            await interaction.SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.");
            return;
        }

        var use = await guild.GetUserAsync(suggest.UserId);
        EmbedBuilder eb;
        if (GetAcceptMessage(guild) is "-" or "" or null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Accepted").WithDescription(suggest.Suggestion).WithOkColor().AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).GetMessageAsync(suggest.MessageId);
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Accepted").WithDescription(desc.Embeds.FirstOrDefault().Description).WithOkColor()
                                       .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction.SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!");
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!");
                return;
            }

            if (GetArchiveOnAccept(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(GetThreadByMessage(suggest.MessageId));
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (await chan.GetMessageAsync(suggest.MessageId) is IUserMessage message)
            {
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                });
                try
                {
                    await message.RemoveAllReactionsAsync();
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                var msg = await chan.SendMessageAsync(embed: eb.Build());
                suggest.MessageId = msg.Id;
                await using var uow = _db.GetDbContext();
                uow.Update(suggest);
                await uow.SaveChangesAsync();
            }

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Accepted");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Accepted By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserId)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted but the user had DMs off.");
            }

            await UpdateSuggestState(suggest, (int)SuggestState.Accepted, user.Id);
            if (GetAcceptChannel(guild) is not 0)
            {
                var acceptChannel = await guild.GetTextChannelAsync(GetAcceptChannel(guild));
                if (acceptChannel is null)
                    return;
                if (!(await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(acceptChannel).EmbedLinks)
                    return;
                if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                {
                    var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                    var toCheck = await guild.GetTextChannelAsync(messageChannel);
                    if (toCheck is not null)
                    {
                        var messageCheck = await toCheck.GetMessageAsync(messageId);
                        if (messageCheck is not null)
                            try
                            {
                                await messageCheck.DeleteAsync();
                            }
                            catch
                            {
                                // ignored
                            }
                    }
                }

                var toSet = await acceptChannel.SendMessageAsync(embed: eb.Build());
                await UpdateStateMessageId(suggest, toSet.Id);
            }
        }
        else
        {
            string sug;
            if (suggest.Suggestion is null)
                sug = (await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).GetMessageAsync(suggest.MessageId)).Embeds.FirstOrDefault().Description;
            else
                sug = suggest.Suggestion;
            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction.SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!");
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!");
                return;
            }

            var message = await chan.GetMessageAsync(suggest.MessageId) as IUserMessage;
            GetSNum(guild.Id);
            var suguse = await guild.GetUserAsync(suggest.UserId);
            var replacer = new ReplacementBuilder().WithServer(client, guild as SocketGuild).WithOverride("%suggest.user%", () => suguse.ToString())
                                                   .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                                                   .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                                                   .WithOverride("%suggest.number%", () => suggest.SuggestionId.ToString())
                                                   .WithOverride("%suggest.user.name%", () => suguse.Username)
                                                   .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                                                   .WithOverride("%suggest.mod.user%", () => user.ToString())
                                                   .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                                                   .WithOverride("%suggest.mod.name%", () => user.Username).WithOverride("%suggest.mod.message%", () => rs)
                                                   .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                                                   .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                                                   .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                                                   .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                                                   .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                                                   .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString()).Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(GetAcceptMessage(guild)), out var embed, out var plainText);
            if (GetArchiveOnAccept(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(GetThreadByMessage(suggest.MessageId));
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (ebe is false)
            {
                if (message is not null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Embed = null;
                        x.Content = replacer.Replace(GetAcceptMessage(guild));
                    });
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(replacer.Replace(GetAcceptMessage(guild)));
                    suggest.MessageId = toReplace.Id;
                    await using var uow = _db.GetDbContext();
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync();
                }

                await UpdateSuggestState(suggest, (int)SuggestState.Accepted, user.Id);
                if (GetAcceptChannel(guild) is not 0)
                {
                    var acceptChannel = await guild.GetTextChannelAsync(GetAcceptChannel(guild));
                    if (acceptChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(acceptChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId);
                                    if (messageCheck is not null)
                                        try
                                        {
                                            await messageCheck.DeleteAsync();
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                }
                            }

                            var toSet = await acceptChannel.SendMessageAsync(replacer.Replace(GetAcceptMessage(guild)));
                            await UpdateStateMessageId(suggest, toSet.Id);
                        }
                    }
                }
            }
            else
            {
                if (message is not null)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = plainText;
                        x.Embed = embed?.Build();
                    });
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(plainText, embed: embed?.Build());
                    await using var uow = _db.GetDbContext();
                    suggest.MessageId = toReplace.Id;
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync();
                }

                if (GetAcceptChannel(guild) is not 0)
                {
                    var acceptChannel = await guild.GetTextChannelAsync(GetAcceptChannel(guild));
                    if (acceptChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(acceptChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild)).Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId);
                                    if (messageCheck is not null)
                                        try
                                        {
                                            await messageCheck.DeleteAsync();
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                }
                            }

                            var toSet = await acceptChannel.SendMessageAsync(plainText, embed: embed?.Build());
                            await UpdateStateMessageId(suggest, toSet.Id);
                        }
                    }
                }
            }

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Accepted");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Accepted By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserId)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted but the user had DMs off.");
            }
        }
    }

    public async Task SendSuggestion(
        IGuild guild,
        IGuildUser user,
        DiscordSocketClient client,
        string suggestion,
        ITextChannel channel,
        IDiscordInteraction? interaction = null)
    {
        if (GetSuggestionChannel(guild.Id) == 0)
        {
            if (interaction is null)
            {
                var msg = await channel.SendErrorAsync("There is no suggestion channel set! Have an admin set it using `setsuggestchannel` and try again!");
                msg.DeleteAfter(3);
                return;
            }

            await interaction.SendEphemeralErrorAsync("There is no suggestion channel set! Have an admin set it using `setsuggestchannel` then try again!");
            return;
        }

        var tup = new Emoji("\uD83D\uDC4D");
        var tdown = new Emoji("\uD83D\uDC4E");
        var emotes = new List<Emote>();
        var em = GetEmotes(guild.Id);
        if (em is not null and not "disable")
        {
            var te = em.Split(",");
            emotes.AddRange(te.Select(Emote.Parse));
        }

        var builder = new ComponentBuilder();
        IEmote[] reacts = { tup, tdown };
        if (GetEmoteMode(guild) == 1)
        {
            var count = 0;
            if (em is null or "disabled")
                foreach (var i in reacts)
                {
                    builder.WithButton("0", $"emotebutton:{count + 1}", emote: i, style: GetButtonStyle(guild, ++count));
                }
            else
                foreach (var i in emotes)
                {
                    builder.WithButton("0", $"emotebutton:{count + 1}", emote: i, style: GetButtonStyle(guild, ++count));
                }
        }

        if (GetThreadType(guild) == 1)
        {
            builder.WithButton("Join/Create Public Discussion", customId: $"publicsuggestthread:{GetSNum(guild.Id)}", ButtonStyle.Secondary, row: 1);
        }

        if (GetThreadType(guild) == 2)
        {
            builder.WithButton("Join/Create Private Discussion", customId: $"privatesuggestthread:{GetSNum(guild.Id)}", ButtonStyle.Secondary, row: 1);
        }

        if (GetSuggestionMessage(guild) is "-" or "")
        {
            var sugnum1 = GetSNum(guild.Id);
            var t = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).SendMessageAsync(
                embed: new EmbedBuilder().WithAuthor(user).WithTitle($"Suggestion #{GetSNum(guild.Id)}").WithDescription(suggestion).WithOkColor().Build(),
                components: builder.Build());
            if (GetEmoteMode(guild) == 0)
            {
                if (em is null or "disabled")
                    foreach (var i in reacts)
                        await t.AddReactionAsync(i);
                else
                    foreach (var ei in emotes)
                        await t.AddReactionAsync(ei);
            }


            await Sugnum(guild, sugnum1 + 1);
            await Suggest(guild, sugnum1, t.Id, user.Id, suggestion);
            if (interaction is not null)
                await interaction.SendEphemeralFollowupConfirmAsync("Suggestion has been sent!");
        }
        else
        {
            var sugnum1 = GetSNum(guild.Id);
            var replacer = new ReplacementBuilder().WithServer(client, guild as SocketGuild).WithOverride("%suggest.user%", user.ToString)
                                                   .WithOverride("%suggest.message%", () => suggestion.SanitizeMentions(true))
                                                   .WithOverride("%suggest.number%", () => sugnum1.ToString()).WithOverride("%suggest.user.name%", () => user.Username)
                                                   .WithOverride("%suggest.user.avatar%", () => user.RealAvatarUrl().ToString()).Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(GetSuggestionMessage(guild)), out var embed, out var plainText);
            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            IUserMessage msg;
            if (ebe is false)
            {
                if (GetEmoteMode(guild) == 1)
                    msg = await chan.SendMessageAsync(replacer.Replace(GetSuggestionMessage(guild)), components: builder.Build());
                else
                    msg = await chan.SendMessageAsync(replacer.Replace(GetSuggestionMessage(guild)));
            }
            else
            {
                if (GetEmoteMode(guild) == 1)
                    msg = await chan.SendMessageAsync(plainText, embed: embed?.Build(), components: builder.Build());
                else
                    msg = await chan.SendMessageAsync(plainText, embed: embed?.Build());
            }

            if (GetEmoteMode(guild) == 0)
            {
                if (em is null or "disabled" or "-")
                    foreach (var i in reacts)
                        await msg.AddReactionAsync(i);
                else
                    foreach (var ei in emotes)
                        await msg.AddReactionAsync(ei);
            }

            await Sugnum(guild, sugnum1 + 1);
            await Suggest(guild, sugnum1, msg.Id, user.Id, suggestion);

            if (interaction is not null)
                await interaction.SendEphemeralConfirmAsync("Suggestion has been sent!");
            else
                await channel.SendConfirmAsync("Suggestion sent!");
        }
    }

    public async Task Suggest(
        IGuild guild,
        ulong suggestId,
        ulong messageId,
        ulong userId,
        string suggestion)
    {
        var guildId = guild.Id;

        var suggest = new SuggestionsModel
        {
            GuildId = guildId,
            SuggestionId = suggestId,
            MessageId = messageId,
            UserId = userId,
            Suggestion = suggestion
        };
        try
        {
            await using var uow = _db.GetDbContext();
            uow.Suggestions.Add(suggest);

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public SuggestionsModel[] Suggestions(ulong gid, ulong sid)
    {
        using var uow = _db.GetDbContext();
        return uow.Suggestions.ForId(gid, sid);
    }

    public List<SuggestionsModel> Suggestions(ulong gid)
    {
        using var uow = _db.GetDbContext();
        return uow.Suggestions.Where(x => x.GuildId == gid).ToList();
    }

    public SuggestionsModel GetSuggestByMessage(ulong msgId)
    {
        using var uow = _db.GetDbContext();
        return uow.Suggestions.FirstOrDefault(x => x.MessageId == msgId);
    }

    public SuggestionsModel[] ForUser(ulong guildId, ulong userId)
    {
        using var uow = _db.GetDbContext();
        return uow.Suggestions.ForUser(guildId, userId);
    }

    public int GetPickedEmote(ulong messageId, ulong userId)
    {
        using var uow = _db.GetDbContext();
        var toreturn = uow.SuggestVotes.FirstOrDefault(x => x.UserId == userId && x.MessageId == messageId);
        return toreturn?.EmotePicked ?? 0;
    }

    public async Task UpdatePickedEmote(ulong messageId, ulong userId, int emotePicked)
    {
        await using var uow = _db.GetDbContext();
        var tocheck = uow.SuggestVotes.FirstOrDefault(x => x.MessageId == messageId && x.UserId == userId);
        if (tocheck is null)
        {
            var toadd = new SuggestVotes() { EmotePicked = emotePicked, MessageId = messageId, UserId = userId };
            uow.SuggestVotes.Add(toadd);
            await uow.SaveChangesAsync();
        }
        else
        {
            tocheck.EmotePicked = emotePicked;
            uow.SuggestVotes.Update(tocheck);
            await uow.SaveChangesAsync();
        }
    }

    public async Task AddThreadChannel(ulong messageId, ulong threadChannelId)
    {
        await using var uow = _db.GetDbContext();
        uow.SuggestThreads.Add(new SuggestThreads() { MessageId = messageId, ThreadChannelId = threadChannelId });
        await uow.SaveChangesAsync();
    }

    public ulong GetThreadByMessage(ulong messageId)
    {
        using var uow = _db.GetDbContext();
        return uow.SuggestThreads.FirstOrDefault(x => x.MessageId == messageId)?.ThreadChannelId ?? 0;
    }
}