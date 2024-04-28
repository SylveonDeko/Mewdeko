using Discord.Net;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Configs;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Serilog;

namespace Mewdeko.Modules.Suggestions.Services;

/// <summary>
/// Manages suggestion operations within a Discord server, facilitating suggestion creation, tracking, and status updates.
/// </summary>
public class SuggestionsService : INService
{
    /// <summary>
    /// Used to track the state of a suggestion.
    /// </summary>
    public enum SuggestState
    {
        /// <summary>
        /// The suggestion is suggested.
        /// </summary>
        Suggested = 0,

        /// <summary>
        /// The suggestion is accepted.
        /// </summary>
        Accepted = 1,

        /// <summary>
        /// The suggestion is denied.
        /// </summary>
        Denied = 2,

        /// <summary>
        /// The suggestion is considered.
        /// </summary>
        Considered = 3,

        /// <summary>
        /// The suggestion is implemented.
        /// </summary>
        Implemented = 4
    }

    private readonly AdministrationService adminserv;
    private readonly DiscordSocketClient client;
    private readonly BotConfig config;
    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;
    private readonly PermissionService perms;
    private readonly List<ulong> repostChecking;
    private readonly List<ulong> spamCheck;

    /// <summary>
    /// Initializes a new instance of the SuggestionsService class.
    /// </summary>
    /// <param name="db">Database service for data persistence.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="aserv">Service for administration tasks.</param>
    /// <param name="permserv">Service for managing permissions.</param>
    /// <param name="guildSettings">Service for guild-specific settings.</param>
    /// <param name="eventHandler">Event handler for Discord client events.</param>
    /// <param name="config">The bot config service.</param>
    public SuggestionsService(
        DbService db,
        DiscordSocketClient client,
        AdministrationService aserv,
        PermissionService permserv,
        GuildSettingsService guildSettings, EventHandler eventHandler, BotConfig config)
    {
        perms = permserv;
        this.guildSettings = guildSettings;
        this.config = config;
        repostChecking = [];
        spamCheck = [];
        adminserv = aserv;
        this.client = client;
        eventHandler.MessageReceived += MessageRecieved;
        eventHandler.ReactionAdded += UpdateCountOnReact;
        eventHandler.ReactionRemoved += UpdateCountOnRemoveReact;
        eventHandler.MessageReceived += RepostButton;
        this.db = db;
    }

    private async Task RepostButton(SocketMessage arg)
    {
        IEnumerable<IMessage> messages;
        if (arg.Channel is not ITextChannel channel)
            return;

        var buttonChannel = await GetSuggestButtonChannel(channel.Guild);
        if (buttonChannel != channel.Id)
            return;

        if (await GetSuggestButtonRepost(channel.Guild) == 0)
            return;
        if (repostChecking.Contains(channel.Id))
            return;
        repostChecking.Add(channel.Id);
        var buttonId = await GetSuggestButtonMessageId(channel.Guild);
        if (await GetSuggestButtonRepost(channel.Guild) is 0)
        {
            repostChecking.Remove(channel.Id);
            return;
        }

        try
        {
            messages = await channel
                .GetMessagesAsync(arg, Direction.Before, await GetSuggestButtonRepost(channel.Guild)).FlattenAsync()
                .ConfigureAwait(false);
        }
        catch (HttpException)
        {
            repostChecking.Remove(channel.Id);
            return;
        }

        if (messages.Select(x => x.Id).Contains(buttonId))
        {
            repostChecking.Remove(channel.Id);
            return;
        }

        if (buttonId != 0)
        {
            try
            {
                await channel.DeleteMessageAsync(buttonId).ConfigureAwait(false);
            }
            catch (HttpException)
            {
                Log.Error($"Button Repost will not work because of missing permissions in guild {channel.Guild}");
                repostChecking.Remove(channel.Id);
                return;
            }
        }

        var message = await GetSuggestButtonMessage(channel.Guild);
        if (string.IsNullOrWhiteSpace(message) || message is "disabled" or "-")
        {
            var eb = new EmbedBuilder().WithOkColor().WithDescription("Press the button below to make a suggestion!");
            var toAdd = await channel
                .SendMessageAsync(embed: eb.Build(), components: (await GetSuggestButton(channel.Guild)).Build())
                .ConfigureAwait(false);
            await SetSuggestionButtonId(channel.Guild, toAdd.Id).ConfigureAwait(false);
            repostChecking.Remove(channel.Id);
            return;
        }

        if (SmartEmbed.TryParse(await GetSuggestButtonMessage(channel.Guild), channel.GuildId, out var embed,
                out var plainText, out _))
        {
            try
            {
                var toadd = await channel.SendMessageAsync(plainText, embeds: embed,
                    components: (await GetSuggestButton(channel.Guild)).Build()).ConfigureAwait(false);
                await SetSuggestionButtonId(channel.Guild, toadd.Id).ConfigureAwait(false);
                repostChecking.Remove(channel.Id);
            }
            catch (NullReferenceException)
            {
                repostChecking.Remove(channel.Id);
            }
        }
        else
        {
            try
            {
                var toadd = await channel.SendMessageAsync(await GetSuggestButtonMessage(channel.Guild),
                        components: (await GetSuggestButton(channel.Guild)).Build())
                    .ConfigureAwait(false);
                await SetSuggestionButtonId(channel.Guild, toadd.Id).ConfigureAwait(false);
                repostChecking.Remove(channel.Id);
            }
            catch (NullReferenceException)
            {
                repostChecking.Remove(channel.Id);
            }
        }

        repostChecking.Remove(channel.Id);
    }

    private async Task UpdateCountOnReact(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2,
        SocketReaction arg3)
    {
        if (await arg2.GetOrDownloadAsync().ConfigureAwait(false) is not ITextChannel channel)
            return;
        if (channel.Id != await GetSuggestionChannel(channel.Guild.Id))
            return;
        var message = await arg1.GetOrDownloadAsync().ConfigureAwait(false);
        if (message is null)
            return;

        var uow = db.GetDbContext();
        await using var _ = uow.ConfigureAwait(false);
        var maybeSuggest =
            uow.Suggestions.FirstOrDefault(x => x.GuildId == channel.GuildId && x.MessageId == message.Id);
        if (maybeSuggest is null)
            return;
        var tup = new Emoji("\uD83D\uDC4D");
        var tdown = new Emoji("\uD83D\uDC4E");
        var toSplit = await GetEmotes(channel.GuildId);
        if (toSplit is "disabled" or "-" or null)
        {
            if (Equals(arg3.Emote, tup))
            {
                maybeSuggest.EmoteCount1 =
                    (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(
                        x => !x.IsBot);
                uow.Suggestions.Update(maybeSuggest);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
            else if (Equals(arg3.Emote, tdown))
            {
                maybeSuggest.EmoteCount2 =
                    (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(
                        x => !x.IsBot);
                uow.Suggestions.Update(maybeSuggest);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
            else
            {
                return;
            }
        }

        var emotes = toSplit.Split(",");
        if (Equals(arg3.Emote, emotes[0].ToIEmote()))
            maybeSuggest.EmoteCount1 =
                (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(x =>
                    !x.IsBot);
        else if (Equals(arg3.Emote, emotes[1].ToIEmote()))
            maybeSuggest.EmoteCount2 =
                (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(x =>
                    !x.IsBot);
        else if (Equals(arg3.Emote, emotes[2].ToIEmote()))
            maybeSuggest.EmoteCount3 =
                (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(x =>
                    !x.IsBot);
        else if (Equals(arg3.Emote, emotes[3].ToIEmote()))
            maybeSuggest.EmoteCount4 =
                (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(x =>
                    !x.IsBot);
        else if (Equals(arg3.Emote, emotes[4].ToIEmote()))
            maybeSuggest.EmoteCount5 =
                (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(x =>
                    !x.IsBot);
        else
            return;

        uow.Suggestions.Update(maybeSuggest);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task UpdateCountOnRemoveReact(Cacheable<IUserMessage, ulong> arg1,
        Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3)
    {
        var message = await arg1.GetOrDownloadAsync().ConfigureAwait(false);
        if (message is null)
            return;

        if (await arg2.GetOrDownloadAsync().ConfigureAwait(false) is not ITextChannel channel)
            return;
        if (channel.Id != await GetSuggestionChannel(channel.Guild.Id))
            return;
        var uow = db.GetDbContext();
        await using var _ = uow.ConfigureAwait(false);
        var maybeSuggest =
            uow.Suggestions.FirstOrDefault(x => x.GuildId == channel.GuildId && x.MessageId == message.Id);
        if (maybeSuggest is null)
            return;
        var tup = new Emoji("\uD83D\uDC4D");
        var tdown = new Emoji("\uD83D\uDC4E");
        var toSplit = await GetEmotes(channel.GuildId);
        if (toSplit is "disabled" or "-" or null)
        {
            if (Equals(arg3.Emote, tup))
            {
                maybeSuggest.EmoteCount1 =
                    (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(
                        x => !x.IsBot);
                uow.Suggestions.Update(maybeSuggest);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
            else if (Equals(arg3.Emote, tdown))
            {
                maybeSuggest.EmoteCount2 =
                    (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(
                        x => !x.IsBot);
                uow.Suggestions.Update(maybeSuggest);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
            else
            {
                return;
            }
        }

        var emotes = toSplit.Split(",");
        if (Equals(arg3.Emote, emotes[0].ToIEmote()))
            maybeSuggest.EmoteCount1 =
                (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(x =>
                    !x.IsBot);
        else if (Equals(arg3.Emote, emotes[1].ToIEmote()))
            maybeSuggest.EmoteCount2 =
                (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(x =>
                    !x.IsBot);
        else if (Equals(arg3.Emote, emotes[2].ToIEmote()))
            maybeSuggest.EmoteCount3 =
                (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(x =>
                    !x.IsBot);
        else if (Equals(arg3.Emote, emotes[3].ToIEmote()))
            maybeSuggest.EmoteCount4 =
                (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(x =>
                    !x.IsBot);
        else if (Equals(arg3.Emote, emotes[4].ToIEmote()))
            maybeSuggest.EmoteCount5 =
                (await message.GetReactionUsersAsync(arg3.Emote, 500).FlattenAsync().ConfigureAwait(false)).Count(x =>
                    !x.IsBot);
        else
            return;

        uow.Suggestions.Update(maybeSuggest);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task MessageRecieved(SocketMessage msg)
    {
        if (msg.Channel is not ITextChannel chan)
            return;
        if (spamCheck.Contains(chan.Id))
            return;
        spamCheck.Add(chan.Id);
        var guild = chan?.Guild;
        var prefix = await guildSettings.GetPrefix(guild);
        if (guild != null && !msg.Author.IsBot && !msg.Content.StartsWith(prefix))
        {
            if (chan.Id != await GetSuggestionChannel(guild.Id))
            {
                spamCheck.Remove(chan.Id);
                return;
            }

            var guser = msg.Author as IGuildUser;
            var pc = await perms.GetCacheFor(guild.Id);
            var test = pc.Permissions.CheckPermissions(msg as IUserMessage, "suggest", "Suggestions".ToLowerInvariant(),
                out _);
            if (!test)
            {
                spamCheck.Remove(chan.Id);
                return;
            }

            if (guser.RoleIds.Contains(await adminserv.GetStaffRole(guser.Guild.Id)))
            {
                spamCheck.Remove(chan.Id);
                return;
            }

            if (msg.Content.Length > await GetMaxLength(guild.Id))
            {
                try
                {
                    await msg.DeleteAsync().ConfigureAwait(false);
                    spamCheck.Remove(chan.Id);
                }
                catch
                {
                    spamCheck.Remove(chan.Id);
                }

                try
                {
                    await guser.SendErrorAsync(
                            $"Cannot send this suggestion as its over the max length `({await GetMaxLength(guild.Id)})` of this server!")
                        .ConfigureAwait(false);
                    spamCheck.Remove(chan.Id);
                }
                catch
                {
                    spamCheck.Remove(chan.Id);
                }

                return;
            }

            if (msg.Content.Length < await GetMinLength(guild.Id))
            {
                try
                {
                    await msg.DeleteAsync().ConfigureAwait(false);
                    spamCheck.Remove(chan.Id);
                }
                catch
                {
                    spamCheck.Remove(chan.Id);
                }

                try
                {
                    await guser.SendErrorAsync(
                            $"Cannot send this suggestion as its under the minimum length `({await GetMaxLength(guild.Id)})` of this server!")
                        .ConfigureAwait(false);
                    spamCheck.Remove(chan.Id);
                }
                catch
                {
                    spamCheck.Remove(chan.Id); // ignore
                }

                return;
            }

            await SendSuggestion(chan.Guild, msg.Author as IGuildUser, client, msg.Content, msg.Channel as ITextChannel)
                .ConfigureAwait(false);
            spamCheck.Remove(chan.Id);
            try
            {
                await msg.DeleteAsync().ConfigureAwait(false);
                spamCheck.Remove(chan.Id);
            }
            catch
            {
                spamCheck.Remove(chan.Id);
            }
        }
        else
            spamCheck.Remove(chan.Id);
    }

    /// <summary>
    /// Gets the current suggestion number for a guild.
    /// </summary>
    /// <param name="id">The unique identifier of the guild.</param>
    /// <returns>The current suggestion number.</returns>
    public async Task<ulong> GetSNum(ulong id) => (await guildSettings.GetGuildConfig(id)).sugnum;

    /// <summary>
    /// Gets the maximum length allowed for suggestions in a guild.
    /// </summary>
    /// <param name="id">The unique identifier of the guild.</param>
    /// <returns>The maximum length of suggestions.</returns>
    public async Task<int> GetMaxLength(ulong id) => (await guildSettings.GetGuildConfig(id)).MaxSuggestLength;

    /// <summary>
    /// Gets the minimum length allowed for suggestions in a guild.
    /// </summary>
    /// <param name="id">The unique identifier of the guild.</param>
    /// <returns>The minimum length of suggestions.</returns>
    public async Task<int> GetMinLength(ulong id) => (await guildSettings.GetGuildConfig(id)).MinSuggestLength;

    /// <summary>
    /// Retrieves the custom emotes set for suggestions in a guild.
    /// </summary>
    /// <param name="id">The unique identifier of the guild.</param>
    /// <returns>A string representing custom emotes.</returns>
    public async Task<string> GetEmotes(ulong id) => (await guildSettings.GetGuildConfig(id)).SuggestEmotes;

    /// <summary>
    /// Sets the button style for suggestion interaction buttons within the guild.
    /// </summary>
    /// <param name="guild">The guild where the setting is to be applied.</param>
    /// <param name="buttonId">The identifier for the specific button to modify.</param>
    /// <param name="color">The color code to set for the button.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetButtonType(IGuild guild, int buttonId, int color)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
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

        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Gets the message and channel IDs for a reposted suggestion, based on its current state.
    /// </summary>
    /// <param name="suggestions">The suggestions model containing the current state.</param>
    /// <param name="guild">The guild from which to retrieve the settings.</param>
    /// <returns>A tuple containing the message ID and channel ID.</returns>
    public async Task<(ulong, ulong)> GetRepostedMessageAndChannel(SuggestionsModel suggestions, IGuild guild)
    {
        (ulong, ulong) toreturn = suggestions.CurrentState switch
        {
            1 => (suggestions.StateChangeMessageId, await GetAcceptChannel(guild)),
            2 => (suggestions.StateChangeMessageId, await GetDenyChannel(guild)),
            3 => (suggestions.StateChangeMessageId, await GetConsiderChannel(guild)),
            4 => (suggestions.StateChangeMessageId, await GetImplementChannel(guild)),
            _ => (0, 0)
        };
        return toreturn;
    }

    /// <summary>
    /// Sets the custom emotes used for suggestions in the guild.
    /// </summary>
    /// <param name="guild">The guild to configure.</param>
    /// <param name="parsedEmotes">A string representation of the custom emotes.</param>
    /// <returns>A task that represents the asynchronous operation of updating the suggestion emotes.</returns>
    public async Task SetSuggestionEmotes(IGuild guild, string parsedEmotes)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.SuggestEmotes = parsedEmotes;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the color for the suggestion button in the guild.
    /// </summary>
    /// <param name="guild">The guild to configure.</param>
    /// <param name="colorNum">The color number to set for the suggestion button.</param>
    /// <returns>A task that represents the asynchronous operation of setting the button color.</returns>
    public async Task SetSuggestButtonColor(IGuild guild, int colorNum)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonColor = colorNum;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Assigns a message ID to the suggestion button for a guild.
    /// </summary>
    /// <param name="guild">The guild to configure.</param>
    /// <param name="messageId">The message ID to associate with the suggestion button.</param>
    /// <returns>A task that represents the asynchronous operation of updating the button message ID.</returns>
    public async Task SetSuggestionButtonId(IGuild guild, ulong messageId)
    {
        var uow = db.GetDbContext();
        await using var _ = uow.ConfigureAwait(false);
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonMessageId = messageId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the channel ID for suggestions in the guild.
    /// </summary>
    /// <param name="guild">The guild to configure.</param>
    /// <param name="channel">The channel ID where suggestions will be posted.</param>
    /// <returns>A task that represents the asynchronous operation of setting the suggestion channel ID.</returns>
    public async Task SetSuggestionChannelId(IGuild guild, ulong channel)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.sugchan = channel;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the minimum length for suggestions in the guild.
    /// </summary>
    /// <param name="guild">The guild to configure.</param>
    /// <param name="minLength">The minimum length allowed for suggestions.</param>
    /// <returns>A task that represents the asynchronous operation of setting the minimum suggestion length.</returns>
    public async Task SetMinLength(IGuild guild, int minLength)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.MinSuggestLength = minLength;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the maximum length for suggestions in the guild.
    /// </summary>
    /// <param name="guild">The guild to configure.</param>
    /// <param name="maxLength">The maximum length allowed for suggestions.</param>
    /// <returns>A task that represents the asynchronous operation of setting the maximum suggestion length.</returns>
    public async Task SetMaxLength(IGuild guild, int maxLength)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.MaxSuggestLength = maxLength;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Generates a ComponentBuilder for the suggestion button with custom settings from the guild.
    /// </summary>
    /// <param name="guild">The guild to retrieve settings from.</param>
    /// <returns>A ComponentBuilder for creating interactive components.</returns>
    public async Task<ComponentBuilder> GetSuggestButton(IGuild guild)
    {
        string buttonLabel;
        IEmote buttonEmote;
        var builder = new ComponentBuilder();
        if (string.IsNullOrWhiteSpace(await GetSuggestButtonName(guild)) ||
            await GetSuggestButtonName(guild) is "disabled" or "-")
            buttonLabel = "Suggest Here!";
        else
            buttonLabel = await GetSuggestButtonName(guild);
        if (string.IsNullOrWhiteSpace(await GetSuggestButtonEmote(guild)) ||
            await GetSuggestButtonEmote(guild) is "disabled" or "-")
            buttonEmote = null;
        else
            buttonEmote = (await GetSuggestButtonEmote(guild)).ToIEmote();
        builder.WithButton(buttonLabel, "suggestbutton", await GetSuggestButtonColor(guild), emote: buttonEmote);
        return builder;
    }

    /// <summary>
    /// Resets the suggestion counter and removes all suggestions in the guild.
    /// </summary>
    /// <param name="guild">The guild where the reset will occur.</param>
    /// <returns>A task that represents the asynchronous operation of resetting suggestions.</returns>
    public async Task SuggestReset(IGuild guild)
    {
        await using var uow = db.GetDbContext();
        await uow.Suggestions.Where(x => x.GuildId == guild.Id).DeleteAsync().ConfigureAwait(false);
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.sugnum = 1;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the message content or embed of the suggestion button in a guild. It allows bypassing the channel check and directly updating or creating a new message if the original message is not found.
    /// </summary>
    /// <param name="guild">The guild to update the suggestion button message in.</param>
    /// <param name="code">The new content or embed code for the suggestion button message.</param>
    /// <param name="bypasschannelcheck">Determines whether to bypass the check for the suggestion button channel setting.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UpdateSuggestionButtonMessage(IGuild guild, string? code, bool bypasschannelcheck = false)
    {
        var toGet = await GetSuggestButtonChannel(guild);
        if (toGet is 0 && !bypasschannelcheck)
            return;
        var channel = await guild.GetTextChannelAsync(toGet).ConfigureAwait(false);
        if (channel is null)
            return;
        var messageId = await GetSuggestButtonMessageId(guild);
        try
        {
            if (messageId is 0)
                messageId = 999;
            var message = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
            if (message is null)
            {
                if (SmartEmbed.TryParse(code, channel.GuildId, out var embed, out var plainText, out _))
                {
                    var toadd = await channel.SendMessageAsync(plainText, embeds: embed,
                        components: (await GetSuggestButton(channel.Guild)).Build()).ConfigureAwait(false);
                    await SetSuggestionButtonId(channel.Guild, toadd.Id).ConfigureAwait(false);
                    return;
                }

                if (code is "-")
                {
                    var eb = new EmbedBuilder().WithOkColor()
                        .WithDescription("Press the button below to make a suggestion!");
                    var toadd = await channel.SendMessageAsync(plainText, embed: eb.Build(),
                        components: (await GetSuggestButton(channel.Guild)).Build()).ConfigureAwait(false);
                    await SetSuggestionButtonId(channel.Guild, toadd.Id).ConfigureAwait(false);
                    return;
                }
                else
                {
                    var toadd = await channel
                        .SendMessageAsync(code, components: (await GetSuggestButton(channel.Guild)).Build())
                        .ConfigureAwait(false);
                    await SetSuggestionButtonId(channel.Guild, toadd.Id).ConfigureAwait(false);
                    return;
                }
            }

            if (code is "-")
            {
                var eb = new EmbedBuilder().WithOkColor()
                    .WithDescription("Press the button below to make a suggestion!");
                try
                {
                    await ((IUserMessage)message).ModifyAsync(async x =>
                    {
                        x.Embed = eb.Build();
                        x.Content = null;
                        x.Components = (await GetSuggestButton(channel.Guild)).Build();
                    }).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            else
            {
                if (SmartEmbed.TryParse(code, channel.GuildId, out var embed, out var plainText, out _))
                {
                    await ((IUserMessage)message).ModifyAsync(async x =>
                    {
                        x.Embeds = embed;
                        x.Content = plainText;
                        x.Components = (await GetSuggestButton(channel.Guild)).Build();
                    }).ConfigureAwait(false);
                }
                else
                {
                    await ((IUserMessage)message).ModifyAsync(async x =>
                    {
                        x.Content = code;
                        x.Embed = null;
                        x.Components = (await GetSuggestButton(channel.Guild)).Build();
                    }).ConfigureAwait(false);
                }
            }
        }
        catch (HttpException)
        {
            // ignored
        }
    }

    /// <summary>
    /// Sets a new message for the suggestion button in a guild. This could be an instruction or information related to making suggestions.
    /// </summary>
    /// <param name="guild">The guild to set the suggestion button message in.</param>
    /// <param name="message">The new message for the suggestion button.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetSuggestButtonMessage(IGuild guild, string? message)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the label for the suggestion button in a guild. This label is displayed on the button itself.
    /// </summary>
    /// <param name="guild">The guild to set the suggestion button label in.</param>
    /// <param name="message">The new label for the suggestion button.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetSuggestButtonLabel(IGuild guild, string message)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonName = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets a new default message that is used when users submit suggestions in a guild. This message can contain placeholders that are replaced with suggestion-specific data.
    /// </summary>
    /// <param name="guild">The guild to set the new default suggestion message in.</param>
    /// <param name="message">The new default message for suggestions.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetSuggestionMessage(IGuild guild, string message)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.SuggestMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets a new default message that is displayed when a suggestion is accepted in a guild.
    /// </summary>
    /// <param name="guild">The guild to set the new default accept message in.</param>
    /// <param name="message">The new default message for when suggestions are accepted.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetAcceptMessage(IGuild guild, string message)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AcceptMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets a new default message that is displayed when a suggestion is denied in a guild.
    /// </summary>
    /// <param name="guild">The guild to set the new default deny message in.</param>
    /// <param name="message">The new default message for when suggestions are denied.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetDenyMessage(IGuild guild, string message)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.DenyMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets a new default message that is displayed when a suggestion is implemented in a guild.
    /// </summary>
    /// <param name="guild">The guild to set the new default implement message in.</param>
    /// <param name="message">The new default message for when suggestions are implemented.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetImplementMessage(IGuild guild, string message)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ImplementMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Updates the state of a suggestion, marking it as accepted, denied, considered, or implemented, and records the user responsible for the state change.
    /// </summary>
    /// <param name="suggestionsModel">The suggestion model to update.</param>
    /// <param name="state">The new state of the suggestion.</param>
    /// <param name="stateChangeId">The ID of the user responsible for the state change.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UpdateSuggestState(SuggestionsModel suggestionsModel, int state, ulong stateChangeId)
    {
        await using var uow = db.GetDbContext();
        suggestionsModel.CurrentState = state;
        suggestionsModel.StateChangeUser = stateChangeId;
        suggestionsModel.StateChangeCount++;
        uow.Suggestions.Update(suggestionsModel);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the message ID associated with the state change of a suggestion. This could refer to the message announcing the suggestion's acceptance, denial, consideration, or implementation.
    /// </summary>
    /// <param name="suggestionsModel">The suggestion model to update.</param>
    /// <param name="messageStateId">The new message ID associated with the state change.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UpdateStateMessageId(SuggestionsModel suggestionsModel, ulong messageStateId)
    {
        await using var uow = db.GetDbContext();
        suggestionsModel.StateChangeMessageId = messageStateId;
        uow.Suggestions.Update(suggestionsModel);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the type of threads that can be created for discussions on suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild to set the thread type in.</param>
    /// <param name="num">The thread type number (e.g., 0 for no threads, 1 for public threads).</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetSuggestThreadsType(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.SuggestionThreadType = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets a new default message that is displayed when a suggestion is under consideration in a guild.
    /// </summary>
    /// <param name="guild">The guild to set the new default consider message in.</param>
    /// <param name="message">The new default message for when suggestions are under consideration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetConsiderMessage(IGuild guild, string message)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ConsiderMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Updates the suggestion number counter in a guild, typically after a new suggestion is added.
    /// </summary>
    /// <param name="guild">The guild to update the suggestion number in.</param>
    /// <param name="num">The new suggestion number.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Sugnum(IGuild guild, ulong num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.sugnum = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets whether suggestions that are denied should be automatically archived in a guild.
    /// </summary>
    /// <param name="guild">The guild to configure the auto-archive setting for denied suggestions.</param>
    /// <param name="value">True if denied suggestions should be archived; otherwise, false.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetArchiveOnDeny(IGuild guild, bool value)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ArchiveOnDeny = value;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the behavior for archiving suggestions upon acceptance within a guild.
    /// </summary>
    /// <param name="guild">The guild to set the archive behavior for.</param>
    /// <param name="value">True to archive suggestions on acceptance; false otherwise.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetArchiveOnAccept(IGuild guild, bool value)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ArchiveOnAccept = value;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the behavior for archiving suggestions upon consideration within a guild.
    /// </summary>
    /// <param name="guild">The guild to set the archive behavior for.</param>
    /// <param name="value">True to archive suggestions on consideration; false otherwise.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetArchiveOnConsider(IGuild guild, bool value)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ArchiveOnConsider = value;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the behavior for archiving suggestions upon implementation within a guild.
    /// </summary>
    /// <param name="guild">The guild to set the archive behavior for.</param>
    /// <param name="value">True to archive suggestions on implementation; false otherwise.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetArchiveOnImplement(IGuild guild, bool value)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ArchiveOnImplement = value;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the emote mode for suggestions within a guild.
    /// </summary>
    /// <param name="guild">The guild to set the emote mode for.</param>
    /// <param name="mode">The emote mode to set.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetEmoteMode(IGuild guild, int mode)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.EmoteMode = mode;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the channel to be used for accepted suggestions within a guild.
    /// </summary>
    /// <param name="guild">The guild to set the accept channel for.</param>
    /// <param name="channelId">The channel ID for accepted suggestions.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetAcceptChannel(IGuild guild, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AcceptChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the channel to be used for denied suggestions within a guild.
    /// </summary>
    /// <param name="guild">The guild to set the deny channel for.</param>
    /// <param name="channelId">The channel ID for denied suggestions.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetDenyChannel(IGuild guild, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.DenyChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the channel to be used for suggestions under consideration within a guild.
    /// </summary>
    /// <param name="guild">The guild to set the consider channel for.</param>
    /// <param name="channelId">The channel ID for suggestions under consideration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetConsiderChannel(IGuild guild, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ConsiderChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the channel to be used for implemented suggestions within a guild.
    /// </summary>
    /// <param name="guild">The guild to set the implement channel for.</param>
    /// <param name="channelId">The channel ID for implemented suggestions.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetImplementChannel(IGuild guild, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ImplementChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the channel to be used for the suggestion button within a guild.
    /// </summary>
    /// <param name="guild">The guild to set the suggestion button channel for.</param>
    /// <param name="channelId">The channel ID for the suggestion button.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetSuggestButtonChannel(IGuild guild, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets the emote to be used on the suggestion button within a guild.
    /// </summary>
    /// <param name="guild">The guild to set the suggestion button emote for.</param>
    /// <param name="emote">The emote for the suggestion button.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetSuggestButtonEmote(IGuild guild, string emote)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.SuggestButtonEmote = emote;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Updates the emote count for a suggestion based on user interactions.
    /// </summary>
    /// <param name="messageId">The message ID of the suggestion.</param>
    /// <param name="emoteNumber">The emote number being updated.</param>
    /// <param name="negative">Indicates whether to decrement (true) or increment (false) the count.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UpdateEmoteCount(ulong messageId, int emoteNumber, bool negative = false)
    {
        await using var uow = db.GetDbContext();
        var suggest = uow.Suggestions.FirstOrDefault(x => x.MessageId == messageId);
        uow.Suggestions.Remove(suggest);
        await uow.SaveChangesAsync().ConfigureAwait(false);
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
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the current count of reactions for a specific emote on a suggestion.
    /// </summary>
    /// <param name="messageId">The message ID of the suggestion.</param>
    /// <param name="emoteNumber">The emote number to retrieve the count for.</param>
    /// <returns>The current count of the specified emote for the suggestion.</returns>
    public async Task<int> GetCurrentCount(ulong messageId, int emoteNumber)
    {
        await using var uow = db.GetDbContext();
        var toupdate = uow.Suggestions.FirstOrDefault(x => x.MessageId == messageId);
        return emoteNumber switch
        {
            1 => toupdate.EmoteCount1,
            2 => toupdate.EmoteCount2,
            3 => toupdate.EmoteCount3,
            4 => toupdate.EmoteCount4,
            5 => toupdate.EmoteCount5,
            _ => 0
        };
    }

    /// <summary>
    /// Retrieves the specific emote used for suggestions within a guild.
    /// </summary>
    /// <param name="guild">The guild to retrieve the emote for.</param>
    /// <param name="num">The number identifying the specific emote.</param>
    /// <returns>The emote used for suggestions in the guild.</returns>
    public async Task<IEmote> GetSuggestMote(IGuild guild, int num)
    {
        var tup = new Emoji("\uD83D\uDC4D");
        var tdown = new Emoji("\uD83D\uDC4E");
        var emotes = (await guildSettings.GetGuildConfig(guild.Id)).SuggestEmotes;
        if (emotes is null or "disabled")
        {
            return num == 1 ? tup : tdown;
        }

        return emotes.Split(",")[num - 1].ToIEmote();
    }

    /// <summary>
    /// Retrieves the ID of the channel designated for suggestions in a guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The channel ID designated for suggestions.</returns>
    public async Task<ulong> GetSuggestionChannel(ulong id)
        => (await guildSettings.GetGuildConfig(id)).sugchan;

    /// <summary>
    /// Retrieves the custom message set for suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the message.</param>
    /// <returns>The custom suggestion message if set; otherwise, null.</returns>
    public async Task<string>? GetSuggestionMessage(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).SuggestMessage;

    /// <summary>
    /// Retrieves the custom accept message for suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the accept message.</param>
    /// <returns>The custom accept message if set; otherwise, null.</returns>
    public async Task<string>? GetAcceptMessage(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).AcceptMessage;

    /// <summary>
    /// Retrieves the custom deny message for suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the deny message.</param>
    /// <returns>The custom deny message if set; otherwise, null.</returns>
    public async Task<string>? GetDenyMessage(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).DenyMessage;

    /// <summary>
    /// Retrieves the custom implement message for suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the implement message.</param>
    /// <returns>The custom implement message if set; otherwise, null.</returns>
    public async Task<string>? GetImplementMessage(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).ImplementMessage;

    /// <summary>
    /// Retrieves the custom consider message for suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the consider message.</param>
    /// <returns>The custom consider message if set; otherwise, null.</returns>
    public async Task<string>? GetConsiderMessage(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).ConsiderMessage;

    /// <summary>
    /// Retrieves the thread type used for suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the thread type.</param>
    /// <returns>The thread type for suggestions.</returns>
    public async Task<int> GetThreadType(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).SuggestionThreadType;

    /// <summary>
    /// Retrieves the emote mode for suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the emote mode.</param>
    /// <returns>The emote mode for suggestions.</returns>
    public async Task<int> GetEmoteMode(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).EmoteMode;

    /// <summary>
    /// Retrieves the channel ID for suggestions under consideration in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the consider channel ID.</param>
    /// <returns>The consider channel ID.</returns>
    public async Task<ulong> GetConsiderChannel(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).ConsiderChannel;

    /// <summary>
    /// Retrieves the channel ID for accepted suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the accept channel ID.</param>
    /// <returns>The accept channel ID.</returns>
    public async Task<ulong> GetAcceptChannel(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).AcceptChannel;

    /// <summary>
    /// Retrieves the channel ID for implemented suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the implement channel ID.</param>
    /// <returns>The implement channel ID.</returns>
    public async Task<ulong> GetImplementChannel(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).ImplementChannel;

    /// <summary>
    /// Retrieves the channel ID for denied suggestions in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the deny channel ID.</param>
    /// <returns>The deny channel ID.</returns>
    public async Task<ulong> GetDenyChannel(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).DenyChannel;

    /// <summary>
    /// Determines whether suggestions are archived upon denial in a guild.
    /// </summary>
    /// <param name="guild">The guild to check the archive setting for.</param>
    /// <returns>True if suggestions are archived upon denial; otherwise, false.</returns>
    public async Task<bool> GetArchiveOnDeny(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).ArchiveOnDeny;

    /// <summary>
    /// Determines whether suggestions are archived upon acceptance in a guild.
    /// </summary>
    /// <param name="guild">The guild to check the archive setting for.</param>
    /// <returns>True if suggestions are archived upon acceptance; otherwise, false.</returns>
    public async Task<bool> GetArchiveOnAccept(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).ArchiveOnAccept;

    /// <summary>
    /// Determines whether suggestions are archived upon consideration in a guild.
    /// </summary>
    /// <param name="guild">The guild to check the archive setting for.</param>
    /// <returns>True if suggestions are archived upon consideration; otherwise, false.</returns>
    public async Task<bool> GetArchiveOnConsider(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).ArchiveOnConsider;

    /// <summary>
    /// Determines whether suggestions are archived upon implementation in a guild.
    /// </summary>
    /// <param name="guild">The guild to check the archive setting for.</param>
    /// <returns>True if suggestions are archived upon implementation; otherwise, false.</returns>
    public async Task<bool> GetArchiveOnImplement(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).ArchiveOnImplement;

    /// <summary>
    /// Retrieves the name of the suggest button in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the suggest button name.</param>
    /// <returns>The name of the suggest button.</returns>
    public async Task<string> GetSuggestButtonName(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).SuggestButtonName;

    /// <summary>
    /// Retrieves the channel ID where the suggest button is located in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the suggest button channel ID.</param>
    /// <returns>The suggest button channel ID.</returns>
    public async Task<ulong> GetSuggestButtonChannel(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).SuggestButtonChannel;

    /// <summary>
    /// Retrieves the custom emote for the suggest button in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the suggest button emote.</param>
    /// <returns>The custom emote for the suggest button; otherwise, null if not set.</returns>
    public async Task<string> GetSuggestButtonEmote(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).SuggestButtonEmote;

    /// <summary>
    /// Retrieves the custom message for the suggest button in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the suggest button message.</param>
    /// <returns>The custom message for the suggest button if set; otherwise, null.</returns>
    public async Task<string>? GetSuggestButtonMessage(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).SuggestButtonMessage;

    /// <summary>
    /// Retrieves the repost threshold for the suggest button in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the suggest button repost threshold.</param>
    /// <returns>The repost threshold for the suggest button.</returns>
    public async Task<int> GetSuggestButtonRepost(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).SuggestButtonRepostThreshold;

    /// <summary>
    /// Retrieves the message ID of the suggest button in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the suggest button message ID.</param>
    /// <returns>The suggest button message ID.</returns>
    public async Task<ulong> GetSuggestButtonMessageId(IGuild guild)
        => (await guildSettings.GetGuildConfig(guild.Id)).SuggestButtonMessageId;

    /// <summary>
    /// Retrieves the color of a suggest button in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the suggest button color.</param>
    /// <returns>The color of the suggest button.</returns>
    public async Task<ButtonStyle> GetSuggestButtonColor(IGuild guild)
        => (ButtonStyle)(await guildSettings.GetGuildConfig(guild.Id)).SuggestButtonColor;

    /// <summary>
    /// Retrieves the style of a specific button based on its ID in a guild.
    /// </summary>
    /// <param name="guild">The guild from which to retrieve the button style.</param>
    /// <param name="id">The ID of the button to retrieve the style for.</param>
    /// <returns>The button style.</returns>
    public async Task<ButtonStyle> GetButtonStyle(IGuild guild, int id) =>
        id switch
        {
            1 => (ButtonStyle)(await guildSettings.GetGuildConfig(guild.Id)).Emote1Style,
            2 => (ButtonStyle)(await guildSettings.GetGuildConfig(guild.Id)).Emote2Style,
            3 => (ButtonStyle)(await guildSettings.GetGuildConfig(guild.Id)).Emote3Style,
            4 => (ButtonStyle)(await guildSettings.GetGuildConfig(guild.Id)).Emote4Style,
            5 => (ButtonStyle)(await guildSettings.GetGuildConfig(guild.Id)).Emote5Style,
            _ => ButtonStyle.Secondary
        };

    /// <summary>
    /// Sends a denial embed for a suggestion in a guild. This method handles fetching the suggestion,
    /// checking if it exists, and then sending a customized embed based on whether a custom deny message
    /// is set. It also handles archiving the suggestion thread if necessary.
    /// </summary>
    /// <param name="guild">The guild where the suggestion is made.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="user">The user who denied the suggestion.</param>
    /// <param name="suggestion">The ID of the suggestion being denied.</param>
    /// <param name="channel">The text channel where the denial message will be sent.</param>
    /// <param name="reason">The reason for denial. Optional.</param>
    /// <param name="interaction">The interaction context, if available. Optional.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task SendDenyEmbed(
        IGuild guild,
        DiscordSocketClient client,
        IUser user,
        ulong suggestion,
        ITextChannel channel,
        string? reason = null,
        IDiscordInteraction? interaction = null)
    {
        try
        {
            var rs = reason ?? "none";
            var suggest = (await Suggestions(guild.Id, suggestion)).FirstOrDefault();
            if (suggest is null)
            {
                if (interaction is null)
                {
                    await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.",
                            config)
                        .ConfigureAwait(false);
                    return;
                }

                await interaction
                    .SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.",
                        config)
                    .ConfigureAwait(false);
                return;
            }

            var use = await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
            EmbedBuilder eb;
            if (await GetDenyMessage(guild) is "-" or "" or null)
            {
                if (suggest.Suggestion != null)
                {
                    eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Denied")
                        .WithDescription(suggest.Suggestion).WithOkColor().AddField("Reason", rs);
                }
                else
                {
                    var desc = await (await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id))
                            .ConfigureAwait(false)).GetMessageAsync(suggest.MessageId)
                        .ConfigureAwait(false);
                    eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Denied")
                        .WithDescription(desc.Embeds.FirstOrDefault()?.Description).WithOkColor()
                        .AddField("Reason", rs);
                }

                var chan = await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id)).ConfigureAwait(false);
                if (chan is null)
                {
                    if (interaction is not null)
                        await interaction
                            .SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!",
                                config)
                            .ConfigureAwait(false);
                    else
                        await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!",
                                config)
                            .ConfigureAwait(false);
                    return;
                }

                if (await GetArchiveOnDeny(guild))
                {
                    var threadChannel = await guild.GetThreadChannelAsync(await GetThreadByMessage(suggest.MessageId))
                        .ConfigureAwait(false);
                    if (threadChannel is not null)
                    {
                        try
                        {
                            await threadChannel.ModifyAsync(x => x.Archived = true).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                if (await chan.GetMessageAsync(suggest.MessageId).ConfigureAwait(false) is IUserMessage message)
                {
                    await message.ModifyAsync(x =>
                    {
                        x.Content = null;
                        x.Embed = eb.Build();
                    }).ConfigureAwait(false);
                    try
                    {
                        await message.RemoveAllReactionsAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
                else
                {
                    var msg = await chan.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                    suggest.MessageId = msg.Id;
                    var uow = db.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    uow.Update(suggest);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
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
                    await (await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false))
                        .SendMessageAsync(embed: emb.Build()).ConfigureAwait(false);
                    if (interaction is null)
                        await channel.SendConfirmAsync("Suggestion set as denied and the user has been dmed.")
                            .ConfigureAwait(false);
                    else
                        await interaction.SendConfirmAsync("Suggestion set as denied and the user has been dmed.")
                            .ConfigureAwait(false);
                }
                catch
                {
                    if (interaction is null)
                        await channel.SendConfirmAsync("Suggestion set as denied but the user had their DMs off.")
                            .ConfigureAwait(false);
                    else
                        await interaction.SendConfirmAsync("Suggestion set as denied but the user had DMs off.")
                            .ConfigureAwait(false);
                }

                await UpdateSuggestState(suggest, (int)SuggestState.Denied, user.Id).ConfigureAwait(false);
                if (await GetDenyChannel(guild) is not 0)
                {
                    var denyChannel =
                        await guild.GetTextChannelAsync(await GetDenyChannel(guild)).ConfigureAwait(false);
                    if (denyChannel is null)
                        return;
                    if (!(await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                        .GetPermissions(denyChannel).EmbedLinks)
                        return;
                    if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false)).Item1 is not 0)
                    {
                        var (messageId, messageChannel) =
                            await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false);
                        var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                        if (toCheck is not null)
                        {
                            var messageCheck = await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                            if (messageCheck is not null)
                            {
                                try
                                {
                                    await messageCheck.DeleteAsync().ConfigureAwait(false);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }
                    }

                    var toSet = await denyChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                    await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
                }
            }
            else
            {
                var sug = suggest.Suggestion ??
                          (await (await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id))
                                  .ConfigureAwait(false)).GetMessageAsync(suggest.MessageId)
                              .ConfigureAwait(false))
                          .Embeds.FirstOrDefault()?.Description;
                var chan = await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id)).ConfigureAwait(false);
                if (chan is null)
                {
                    if (interaction is not null)
                        await interaction
                            .SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!",
                                config)
                            .ConfigureAwait(false);
                    else
                        await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!",
                                config)
                            .ConfigureAwait(false);
                    return;
                }

                var message = await chan.GetMessageAsync(suggest.MessageId).ConfigureAwait(false) as IUserMessage;
                var suguse = await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
                var replacer = new ReplacementBuilder().WithServer(client, guild as SocketGuild)
                    .WithOverride("%suggest.user%", () => suguse.ToString())
                    .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                    .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                    .WithOverride("%suggest.number%", () => suggest.SuggestionId.ToString())
                    .WithOverride("%suggest.user.name%", () => suguse.Username)
                    .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                    .WithOverride("%suggest.mod.user%", user.ToString)
                    .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                    .WithOverride("%suggest.mod.name%", () => user.Username)
                    .WithOverride("%suggest.mod.message%", () => rs)
                    .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                    .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                    .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                    .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                    .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                    .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString()).Build();
                var ebe = SmartEmbed.TryParse(replacer.Replace(await GetDenyMessage(guild)), chan.GuildId,
                    out var embed, out var plainText, out _);
                if (await GetArchiveOnDeny(guild))
                {
                    var threadChannel = await guild.GetThreadChannelAsync(await GetThreadByMessage(suggest.MessageId))
                        .ConfigureAwait(false);
                    if (threadChannel is not null)
                    {
                        try
                        {
                            await threadChannel.ModifyAsync(x => x.Archived = true).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                if (!ebe)
                {
                    if (message is not null)
                    {
                        await message.ModifyAsync(async x =>
                        {
                            x.Embed = null;
                            x.Content = replacer.Replace(await GetDenyMessage(guild));
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        var toReplace = await chan.SendMessageAsync(replacer.Replace(await GetDenyMessage(guild)))
                            .ConfigureAwait(false);
                        suggest.MessageId = toReplace.Id;
                        var uow = db.GetDbContext();
                        await using var _ = uow.ConfigureAwait(false);
                        uow.Suggestions.Update(suggest);
                        await uow.SaveChangesAsync().ConfigureAwait(false);
                    }

                    await UpdateSuggestState(suggest, (int)SuggestState.Denied, user.Id).ConfigureAwait(false);
                    if (await GetDenyChannel(guild) is not 0)
                    {
                        var denyChannel = await guild.GetTextChannelAsync(await GetDenyChannel(guild))
                            .ConfigureAwait(false);
                        if (denyChannel is not null)
                        {
                            if ((await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                                .GetPermissions(denyChannel).EmbedLinks)
                            {
                                if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false))
                                    .Item1 is not 0)
                                {
                                    var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild)
                                        .ConfigureAwait(false);
                                    var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                                    if (toCheck is not null)
                                    {
                                        var messageCheck =
                                            await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                                        if (messageCheck is not null)
                                        {
                                            try
                                            {
                                                await messageCheck.DeleteAsync().ConfigureAwait(false);
                                            }
                                            catch
                                            {
                                                // ignored
                                            }
                                        }
                                    }
                                }

                                var toSet = await denyChannel
                                    .SendMessageAsync(replacer.Replace(await GetDenyMessage(guild)))
                                    .ConfigureAwait(false);
                                await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
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
                            x.Embeds = embed;
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        var toReplace = await chan.SendMessageAsync(plainText, embeds: embed).ConfigureAwait(false);
                        var uow = db.GetDbContext();
                        await using var _ = uow.ConfigureAwait(false);
                        suggest.MessageId = toReplace.Id;
                        uow.Suggestions.Update(suggest);
                        await uow.SaveChangesAsync().ConfigureAwait(false);
                    }

                    if (await GetDenyChannel(guild) is not 0)
                    {
                        var denyChannel = await guild.GetTextChannelAsync(await GetDenyChannel(guild))
                            .ConfigureAwait(false);
                        if (denyChannel is not null)
                        {
                            if ((await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                                .GetPermissions(denyChannel).EmbedLinks)
                            {
                                if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false))
                                    .Item1 is not 0)
                                {
                                    var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild)
                                        .ConfigureAwait(false);
                                    var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                                    if (toCheck is not null)
                                    {
                                        var messageCheck =
                                            await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                                        if (messageCheck is not null)
                                        {
                                            try
                                            {
                                                await messageCheck.DeleteAsync().ConfigureAwait(false);
                                            }
                                            catch
                                            {
                                                // ignored
                                            }
                                        }
                                    }
                                }

                                var toSet = await denyChannel.SendMessageAsync(plainText, embeds: embed)
                                    .ConfigureAwait(false);
                                await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
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
                    await (await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false))
                        .SendMessageAsync(embed: emb.Build()).ConfigureAwait(false);
                    if (interaction is null)
                        await channel
                            .SendConfirmAsync("Suggestion set as denied and the user has been dmed the denial!")
                            .ConfigureAwait(false);
                    else
                        await interaction.SendConfirmAsync("Suggestion set as denied and the user has been dmed.")
                            .ConfigureAwait(false);
                }
                catch
                {
                    if (interaction is null)
                        await channel.SendConfirmAsync("Suggestion set as denied but the user had their dms off.")
                            .ConfigureAwait(false);
                    else
                        await interaction.SendConfirmAsync("Suggestion set as denied but the user had DMs off.")
                            .ConfigureAwait(false);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// Sends a consideration embed for a suggestion in a guild. Similar to denial, it sends an embed indicating
    /// the suggestion is under consideration, with customized messages if set, and handles archiving.
    /// </summary>
    /// <param name="guild">The guild where the suggestion is made.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="user">The user considering the suggestion.</param>
    /// <param name="suggestion">The ID of the suggestion being considered.</param>
    /// <param name="channel">The channel where the consideration message will be sent.</param>
    /// <param name="reason">The reason for consideration. Optional.</param>
    /// <param name="interaction">The interaction context, if available. Optional.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task SendConsiderEmbed(
        IGuild guild,
        DiscordSocketClient client,
        IUser user,
        ulong suggestion,
        ITextChannel channel,
        string? reason = null,
        IDiscordInteraction? interaction = null)
    {
        var rs = reason ?? "none";
        var suggest = (await Suggestions(guild.Id, suggestion)).FirstOrDefault();
        if (suggest.Suggestion is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.",
                        config)
                    .ConfigureAwait(false);
                return;
            }

            await interaction
                .SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.", config)
                .ConfigureAwait(false);
            return;
        }

        var use = await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
        EmbedBuilder eb;
        if (await GetConsiderMessage(guild) is "-" or "" or null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Considering")
                    .WithDescription(suggest.Suggestion).WithOkColor().AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id))
                        .ConfigureAwait(false)).GetMessageAsync(suggest.MessageId)
                    .ConfigureAwait(false);
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Considering")
                    .WithDescription(desc.Embeds.FirstOrDefault().Description).WithOkColor()
                    .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id)).ConfigureAwait(false);
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction
                        .SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                return;
            }

            if (await GetArchiveOnConsider(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(await GetThreadByMessage(suggest.MessageId))
                    .ConfigureAwait(false);
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (await chan.GetMessageAsync(suggest.MessageId).ConfigureAwait(false) is IUserMessage message)
            {
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                }).ConfigureAwait(false);
                try
                {
                    await message.RemoveAllReactionsAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                var msg = await chan.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                suggest.MessageId = msg.Id;
                var uow = db.GetDbContext();
                await using var _ = uow.ConfigureAwait(false);
                uow.Update(suggest);
                await uow.SaveChangesAsync().ConfigureAwait(false);
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
                await (await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false))
                    .SendMessageAsync(embed: emb.Build()).ConfigureAwait(false);
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered and the user has been dmed.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered and the user has been dmed.")
                        .ConfigureAwait(false);
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered but the user had their dms off.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered but the user had DMs off.")
                        .ConfigureAwait(false);
            }

            await UpdateSuggestState(suggest, (int)SuggestState.Considered, user.Id).ConfigureAwait(false);
            if (await GetConsiderChannel(guild) is not 0)
            {
                var considerChannel =
                    await guild.GetTextChannelAsync(await GetConsiderChannel(guild)).ConfigureAwait(false);
                if (considerChannel is null)
                    return;
                if (!(await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                    .GetPermissions(considerChannel).EmbedLinks)
                    return;
                if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false)).Item1 is not 0)
                {
                    var (messageId, messageChannel) =
                        await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false);
                    var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                    if (toCheck is not null)
                    {
                        var messageCheck = await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                        if (messageCheck is not null)
                        {
                            try
                            {
                                await messageCheck.DeleteAsync().ConfigureAwait(false);
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                }

                var toSet = await considerChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
            }
        }
        else
        {
            var sug = suggest.Suggestion ??
                      (await (await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id))
                          .ConfigureAwait(false)).GetMessageAsync(suggest.MessageId).ConfigureAwait(false))
                      .Embeds.FirstOrDefault()!.Description;
            var chan = await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id)).ConfigureAwait(false);
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction
                        .SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                return;
            }

            var message = await chan.GetMessageAsync(suggest.MessageId).ConfigureAwait(false) as IUserMessage;
            var suguse = await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
            var replacer = new ReplacementBuilder().WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", () => suguse.ToString())
                .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                .WithOverride("%suggest.number%", () => suggest.SuggestionId.ToString())
                .WithOverride("%suggest.user.name%", () => suguse.Username)
                .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                .WithOverride("%suggest.mod.user%", user.ToString)
                .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                .WithOverride("%suggest.mod.name%", () => user.Username).WithOverride("%suggest.mod.message%", () => rs)
                .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString()).Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(await GetConsiderMessage(guild)), chan.GuildId,
                out var embed, out var plainText, out _);
            if (await GetArchiveOnConsider(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(await GetThreadByMessage(suggest.MessageId))
                    .ConfigureAwait(false);
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (!ebe)
            {
                if (message is not null)
                {
                    await message.ModifyAsync(async x =>
                    {
                        x.Embed = null;
                        x.Content = replacer.Replace(await GetConsiderMessage(guild));
                    }).ConfigureAwait(false);
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(replacer.Replace(await GetConsiderMessage(guild)))
                        .ConfigureAwait(false);
                    suggest.MessageId = toReplace.Id;
                    var uow = db.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }

                await UpdateSuggestState(suggest, (int)SuggestState.Considered, user.Id).ConfigureAwait(false);
                if (await GetConsiderChannel(guild) is not 0)
                {
                    var considerChannel = await guild.GetTextChannelAsync(await GetConsiderChannel(guild))
                        .ConfigureAwait(false);
                    if (considerChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                            .GetPermissions(considerChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false))
                                .Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild)
                                    .ConfigureAwait(false);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                                    if (messageCheck is not null)
                                    {
                                        try
                                        {
                                            await messageCheck.DeleteAsync().ConfigureAwait(false);
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                    }
                                }
                            }

                            var toSet = await considerChannel
                                .SendMessageAsync(replacer.Replace(await GetConsiderMessage(guild)))
                                .ConfigureAwait(false);
                            await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
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
                        x.Embeds = embed;
                    }).ConfigureAwait(false);
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(plainText, embeds: embed).ConfigureAwait(false);
                    var uow = db.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    suggest.MessageId = toReplace.Id;
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }

                if (await GetConsiderChannel(guild) is not 0)
                {
                    var considerChannel = await guild.GetTextChannelAsync(await GetConsiderChannel(guild))
                        .ConfigureAwait(false);
                    if (considerChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                            .GetPermissions(considerChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false))
                                .Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild)
                                    .ConfigureAwait(false);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                                    if (messageCheck is not null)
                                    {
                                        try
                                        {
                                            await messageCheck.DeleteAsync().ConfigureAwait(false);
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                    }
                                }
                            }

                            var toSet = await considerChannel.SendMessageAsync(plainText, embeds: embed)
                                .ConfigureAwait(false);
                            await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
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
                await (await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false))
                    .SendMessageAsync(embed: emb.Build()).ConfigureAwait(false);
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered and the user has been dmed.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered and the user has been dmed.")
                        .ConfigureAwait(false);
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered but the user had their dms off.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered but the user had DMs off.")
                        .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Sends an implementation embed for a suggestion in a guild. This method handles the entire process
    /// of marking a suggestion as implemented, including sending a customized embed message, and managing
    /// the suggestion's thread.
    /// </summary>
    /// <param name="guild">The guild where the suggestion is made.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="user">The user who implemented the suggestion.</param>
    /// <param name="suggestion">The ID of the suggestion being implemented.</param>
    /// <param name="channel">The channel where the implementation message will be sent.</param>
    /// <param name="reason">The reason for implementation. Optional.</param>
    /// <param name="interaction">The interaction context, if available. Optional.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task SendImplementEmbed(
        IGuild guild,
        DiscordSocketClient client,
        IUser user,
        ulong suggestion,
        ITextChannel channel,
        string? reason = null,
        IDiscordInteraction? interaction = null)
    {
        var rs = reason ?? "none";
        var suggest = (await Suggestions(guild.Id, suggestion)).FirstOrDefault();
        if (suggest.Suggestion is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.",
                        config)
                    .ConfigureAwait(false);
                return;
            }

            await interaction
                .SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.", config)
                .ConfigureAwait(false);
            return;
        }

        var use = await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
        EmbedBuilder eb;
        if (await GetImplementMessage(guild) is "-" or "" or null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Implemented")
                    .WithDescription(suggest.Suggestion).WithOkColor().AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id))
                        .ConfigureAwait(false)).GetMessageAsync(suggest.MessageId)
                    .ConfigureAwait(false);
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Implemented")
                    .WithDescription(desc.Embeds.FirstOrDefault().Description).WithOkColor()
                    .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id)).ConfigureAwait(false);
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction
                        .SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                return;
            }

            if (await GetArchiveOnImplement(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(await GetThreadByMessage(suggest.MessageId))
                    .ConfigureAwait(false);
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (await chan.GetMessageAsync(suggest.MessageId).ConfigureAwait(false) is IUserMessage message)
            {
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                }).ConfigureAwait(false);
                try
                {
                    await message.RemoveAllReactionsAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                var msg = await chan.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                suggest.MessageId = msg.Id;
                var uow = db.GetDbContext();
                await using var _ = uow.ConfigureAwait(false);
                uow.Update(suggest);
                await uow.SaveChangesAsync().ConfigureAwait(false);
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
                await (await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false))
                    .SendMessageAsync(embed: emb.Build()).ConfigureAwait(false);
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.")
                        .ConfigureAwait(false);
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented but the user had their dms off.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented but the user had DMs off.")
                        .ConfigureAwait(false);
            }

            await UpdateSuggestState(suggest, (int)SuggestState.Implemented, user.Id).ConfigureAwait(false);
            if (await GetImplementChannel(guild) is not 0)
            {
                var implementChannel =
                    await guild.GetTextChannelAsync(await GetImplementChannel(guild)).ConfigureAwait(false);
                if (implementChannel is null)
                    return;
                if (!(await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                    .GetPermissions(implementChannel).EmbedLinks)
                    return;
                if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false)).Item1 is not 0)
                {
                    var (messageId, messageChannel) =
                        await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false);
                    var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                    if (toCheck is not null)
                    {
                        var messageCheck = await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                        if (messageCheck is not null)
                        {
                            try
                            {
                                await messageCheck.DeleteAsync().ConfigureAwait(false);
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                }

                var toSet = await implementChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
            }
        }
        else
        {
            var sug = suggest.Suggestion ??
                      (await (await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id))
                          .ConfigureAwait(false)).GetMessageAsync(suggest.MessageId).ConfigureAwait(false))
                      .Embeds.FirstOrDefault()?.Description;
            var chan = await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id)).ConfigureAwait(false);
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction
                        .SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                return;
            }

            var message = await chan.GetMessageAsync(suggest.MessageId).ConfigureAwait(false) as IUserMessage;
            var suguse = await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
            var replacer = new ReplacementBuilder().WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", () => suguse.ToString())
                .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                .WithOverride("%suggest.number%", () => suggest.SuggestionId.ToString())
                .WithOverride("%suggest.user.name%", () => suguse.Username)
                .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                .WithOverride("%suggest.mod.user%", user.ToString)
                .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                .WithOverride("%suggest.mod.name%", () => user.Username).WithOverride("%suggest.mod.message%", () => rs)
                .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString()).Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(await GetImplementMessage(guild)), chan.GuildId,
                out var embed, out var plainText, out _);
            if (await GetArchiveOnImplement(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(await GetThreadByMessage(suggest.MessageId))
                    .ConfigureAwait(false);
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (!ebe)
            {
                if (message is not null)
                {
                    await message.ModifyAsync(async x =>
                    {
                        x.Embed = null;
                        x.Content = replacer.Replace(await GetImplementMessage(guild));
                    }).ConfigureAwait(false);
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(replacer.Replace(await GetImplementMessage(guild)))
                        .ConfigureAwait(false);
                    suggest.MessageId = toReplace.Id;
                    var uow = db.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }

                await UpdateSuggestState(suggest, (int)SuggestState.Implemented, user.Id).ConfigureAwait(false);
                if (await GetImplementChannel(guild) is not 0)
                {
                    var implementChannel = await guild.GetTextChannelAsync(await GetImplementChannel(guild))
                        .ConfigureAwait(false);
                    if (implementChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                            .GetPermissions(implementChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false))
                                .Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild)
                                    .ConfigureAwait(false);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                                    if (messageCheck is not null)
                                    {
                                        try
                                        {
                                            await messageCheck.DeleteAsync().ConfigureAwait(false);
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                    }
                                }
                            }

                            var toSet = await implementChannel
                                .SendMessageAsync(replacer.Replace(await GetImplementMessage(guild)))
                                .ConfigureAwait(false);
                            await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
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
                        x.Embeds = embed;
                    }).ConfigureAwait(false);
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(plainText, embeds: embed).ConfigureAwait(false);
                    var uow = db.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    suggest.MessageId = toReplace.Id;
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }

                if (await GetImplementChannel(guild) is not 0)
                {
                    var implementChannel = await guild.GetTextChannelAsync(await GetImplementChannel(guild))
                        .ConfigureAwait(false);
                    if (implementChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                            .GetPermissions(implementChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false))
                                .Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild)
                                    .ConfigureAwait(false);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                                    if (messageCheck is not null)
                                    {
                                        try
                                        {
                                            await messageCheck.DeleteAsync().ConfigureAwait(false);
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                    }
                                }
                            }

                            var toSet = await implementChannel.SendMessageAsync(plainText, embeds: embed)
                                .ConfigureAwait(false);
                            await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
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
                await (await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false))
                    .SendMessageAsync(embed: emb.Build()).ConfigureAwait(false);
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.")
                        .ConfigureAwait(false);
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented but the user had their dms off.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented but the user had DMs off.")
                        .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Sends an acceptance embed for a suggestion in a guild. It manages sending a custom embed for suggestions
    /// marked as accepted, archiving the thread if set, and notifying the suggestion's author.
    /// </summary>
    /// <param name="guild">The guild where the suggestion is made.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="user">The user who accepted the suggestion.</param>
    /// <param name="suggestion">The ID of the suggestion being accepted.</param>
    /// <param name="channel">The channel where the acceptance message will be sent.</param>
    /// <param name="reason">The reason for acceptance. Optional.</param>
    /// <param name="interaction">The interaction context, if available. Optional.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
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
        var suggest = (await Suggestions(guild.Id, suggestion)).FirstOrDefault();
        if (suggest.Suggestion is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.",
                        config)
                    .ConfigureAwait(false);
                return;
            }

            await interaction
                .SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.", config)
                .ConfigureAwait(false);
            return;
        }

        var use = await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
        EmbedBuilder eb;
        if (await GetAcceptMessage(guild) is "-" or "" or null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Accepted")
                    .WithDescription(suggest.Suggestion).WithOkColor().AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id))
                        .ConfigureAwait(false)).GetMessageAsync(suggest.MessageId)
                    .ConfigureAwait(false);
                eb = new EmbedBuilder().WithAuthor(use).WithTitle($"Suggestion #{suggestion} Accepted")
                    .WithDescription(desc.Embeds.FirstOrDefault().Description).WithOkColor()
                    .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id)).ConfigureAwait(false);
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction
                        .SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                return;
            }

            if (await GetArchiveOnAccept(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(await GetThreadByMessage(suggest.MessageId))
                    .ConfigureAwait(false);
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (await chan.GetMessageAsync(suggest.MessageId).ConfigureAwait(false) is IUserMessage message)
            {
                await message.ModifyAsync(x =>
                {
                    x.Content = null;
                    x.Embed = eb.Build();
                }).ConfigureAwait(false);
                try
                {
                    await message.RemoveAllReactionsAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
            else
            {
                var msg = await chan.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                suggest.MessageId = msg.Id;
                var uow = db.GetDbContext();
                await using var _ = uow.ConfigureAwait(false);
                uow.Update(suggest);
                await uow.SaveChangesAsync().ConfigureAwait(false);
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
                await (await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false))
                    .SendMessageAsync(embed: emb.Build()).ConfigureAwait(false);
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.")
                        .ConfigureAwait(false);
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted but the user had their dms off.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted but the user had DMs off.")
                        .ConfigureAwait(false);
            }

            await UpdateSuggestState(suggest, (int)SuggestState.Accepted, user.Id).ConfigureAwait(false);
            if (await GetAcceptChannel(guild) is not 0)
            {
                var acceptChannel =
                    await guild.GetTextChannelAsync(await GetAcceptChannel(guild)).ConfigureAwait(false);
                if (acceptChannel is null)
                    return;
                if (!(await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                    .GetPermissions(acceptChannel).EmbedLinks)
                    return;
                if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false)).Item1 is not 0)
                {
                    var (messageId, messageChannel) =
                        await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false);
                    var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                    if (toCheck is not null)
                    {
                        var messageCheck = await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                        if (messageCheck is not null)
                        {
                            try
                            {
                                await messageCheck.DeleteAsync().ConfigureAwait(false);
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                }

                var toSet = await acceptChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
            }
        }
        else
        {
            var sug = suggest.Suggestion ??
                      (await (await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id))
                          .ConfigureAwait(false)).GetMessageAsync(suggest.MessageId).ConfigureAwait(false))
                      .Embeds.FirstOrDefault().Description;
            var chan = await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id)).ConfigureAwait(false);
            if (chan is null)
            {
                if (interaction is not null)
                    await interaction
                        .SendEphemeralErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                else
                    await channel.SendErrorAsync("The suggestion channel is invalid, please set it and try again!",
                            config)
                        .ConfigureAwait(false);
                return;
            }

            var message = await chan.GetMessageAsync(suggest.MessageId).ConfigureAwait(false) as IUserMessage;
            await GetSNum(guild.Id);
            var suguse = await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
            var replacer = new ReplacementBuilder().WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", () => suguse.ToString())
                .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                .WithOverride("%suggest.number%", () => suggest.SuggestionId.ToString())
                .WithOverride("%suggest.user.name%", () => suguse.Username)
                .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                .WithOverride("%suggest.mod.user%", user.ToString)
                .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                .WithOverride("%suggest.mod.name%", () => user.Username).WithOverride("%suggest.mod.message%", () => rs)
                .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString()).Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(await GetAcceptMessage(guild)), chan.GuildId, out var embed,
                out var plainText, out _);
            if (await GetArchiveOnAccept(guild))
            {
                var threadChannel = await guild.GetThreadChannelAsync(await GetThreadByMessage(suggest.MessageId))
                    .ConfigureAwait(false);
                if (threadChannel is not null)
                {
                    try
                    {
                        await threadChannel.ModifyAsync(x => x.Archived = true).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (!ebe)
            {
                if (message is not null)
                {
                    await message.ModifyAsync(async x =>
                    {
                        x.Embed = null;
                        x.Content = replacer.Replace(await GetAcceptMessage(guild));
                    }).ConfigureAwait(false);
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(replacer.Replace(await GetAcceptMessage(guild)))
                        .ConfigureAwait(false);
                    suggest.MessageId = toReplace.Id;
                    var uow = db.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }

                await UpdateSuggestState(suggest, (int)SuggestState.Accepted, user.Id).ConfigureAwait(false);
                if (await GetAcceptChannel(guild) is not 0)
                {
                    var acceptChannel =
                        await guild.GetTextChannelAsync(await GetAcceptChannel(guild)).ConfigureAwait(false);
                    if (acceptChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                            .GetPermissions(acceptChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false))
                                .Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild)
                                    .ConfigureAwait(false);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                                    if (messageCheck is not null)
                                    {
                                        try
                                        {
                                            await messageCheck.DeleteAsync().ConfigureAwait(false);
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                    }
                                }
                            }

                            var toSet = await acceptChannel
                                .SendMessageAsync(replacer.Replace(await GetAcceptMessage(guild)))
                                .ConfigureAwait(false);
                            await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
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
                        x.Embeds = embed;
                    }).ConfigureAwait(false);
                }
                else
                {
                    var toReplace = await chan.SendMessageAsync(plainText, embeds: embed).ConfigureAwait(false);
                    var uow = db.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    suggest.MessageId = toReplace.Id;
                    uow.Suggestions.Update(suggest);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                }

                if (await GetAcceptChannel(guild) is not 0)
                {
                    var acceptChannel =
                        await guild.GetTextChannelAsync(await GetAcceptChannel(guild)).ConfigureAwait(false);
                    if (acceptChannel is not null)
                    {
                        if ((await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false))
                            .GetPermissions(acceptChannel).EmbedLinks)
                        {
                            if ((await GetRepostedMessageAndChannel(suggest, guild).ConfigureAwait(false))
                                .Item1 is not 0)
                            {
                                var (messageId, messageChannel) = await GetRepostedMessageAndChannel(suggest, guild)
                                    .ConfigureAwait(false);
                                var toCheck = await guild.GetTextChannelAsync(messageChannel).ConfigureAwait(false);
                                if (toCheck is not null)
                                {
                                    var messageCheck = await toCheck.GetMessageAsync(messageId).ConfigureAwait(false);
                                    if (messageCheck is not null)
                                    {
                                        try
                                        {
                                            await messageCheck.DeleteAsync().ConfigureAwait(false);
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                    }
                                }
                            }

                            var toSet = await acceptChannel.SendMessageAsync(plainText, embeds: embed)
                                .ConfigureAwait(false);
                            await UpdateStateMessageId(suggest, toSet.Id).ConfigureAwait(false);
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
                await (await guild.GetUserAsync(suggest.UserId).ConfigureAwait(false))
                    .SendMessageAsync(embed: emb.Build()).ConfigureAwait(false);
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.")
                        .ConfigureAwait(false);
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted but the user had their dms off.")
                        .ConfigureAwait(false);
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted but the user had DMs off.")
                        .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Submits a suggestion in a guild, handling the creation of suggestion messages with reactions or buttons
    /// based on configuration, and potentially starting a thread for discussion.
    /// </summary>
    /// <param name="guild">The guild where the suggestion is submitted.</param>
    /// <param name="user">The user submitting the suggestion.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="suggestion">The text of the suggestion being submitted.</param>
    /// <param name="channel">The channel where the suggestion submission confirmation will be sent.</param>
    /// <param name="interaction">The interaction context, if available. Optional.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task SendSuggestion(
        IGuild guild,
        IGuildUser user,
        DiscordSocketClient client,
        string suggestion,
        ITextChannel channel,
        IDiscordInteraction? interaction = null)
    {
        if (await GetSuggestionChannel(guild.Id) == 0)
        {
            if (interaction is null)
            {
                var msg = await channel
                    .SendErrorAsync(
                        "There is no suggestion channel set! Have an admin set it using `setsuggestchannel` and try again!",
                        config)
                    .ConfigureAwait(false);
                msg.DeleteAfter(3);
                return;
            }

            await interaction
                .SendEphemeralErrorAsync(
                    "There is no suggestion channel set! Have an admin set it using `setsuggestchannel` then try again!",
                    config)
                .ConfigureAwait(false);
            return;
        }

        var tup = new Emoji("\uD83D\uDC4D");
        var tdown = new Emoji("\uD83D\uDC4E");
        var emotes = new List<Emote>();
        var em = await GetEmotes(guild.Id);
        if (em is not null and not "disable")
        {
            var te = em.Split(",");
            emotes.AddRange(te.Select(Emote.Parse));
        }

        var builder = new ComponentBuilder();
        IEmote[] reacts =
        [
            tup, tdown
        ];
        if (await GetEmoteMode(guild) == 1)
        {
            var count = 0;
            if (em is null or "disabled")
            {
                foreach (var i in reacts)
                {
                    builder.WithButton("0", $"emotebutton:{count + 1}", emote: i,
                        style: await GetButtonStyle(guild, ++count));
                }
            }
            else
            {
                foreach (var i in emotes)
                {
                    builder.WithButton("0", $"emotebutton:{count + 1}", emote: i,
                        style: await GetButtonStyle(guild, ++count));
                }
            }
        }

        var snum = await GetSNum(guild.Id);
        if (await GetThreadType(guild) == 1)
        {
            builder.WithButton("Join/Create Public Discussion", customId: $"publicsuggestthread:{snum}",
                ButtonStyle.Secondary, row: 1);
        }

        if (await GetThreadType(guild) == 2)
        {
            builder.WithButton("Join/Create Private Discussion", customId: $"privatesuggestthread:{snum}",
                ButtonStyle.Secondary, row: 1);
        }

        if (await GetSuggestionMessage(guild) is "-" or "")
        {
            var sugnum1 = await GetSNum(guild.Id);
            var t = await (await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id)).ConfigureAwait(false))
                .SendMessageAsync(
                    embed: new EmbedBuilder().WithAuthor(user).WithTitle($"Suggestion #{sugnum1}")
                        .WithDescription(suggestion).WithOkColor().Build(),
                    components: builder.Build()).ConfigureAwait(false);
            if (await GetEmoteMode(guild) == 0)
            {
                if (em is null or "disabled")
                {
                    foreach (var i in reacts)
                        await t.AddReactionAsync(i).ConfigureAwait(false);
                }
                else
                {
                    foreach (var ei in emotes)
                        await t.AddReactionAsync(ei).ConfigureAwait(false);
                }
            }

            await Sugnum(guild, sugnum1 + 1).ConfigureAwait(false);
            await Suggest(guild, sugnum1, t.Id, user.Id, suggestion).ConfigureAwait(false);
            if (interaction is not null)
                await interaction.SendEphemeralFollowupConfirmAsync("Suggestion has been sent!").ConfigureAwait(false);
        }
        else
        {
            var sugnum1 = await GetSNum(guild.Id);
            var replacer = new ReplacementBuilder().WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", user.ToString)
                .WithOverride("%suggest.message%", () => suggestion.SanitizeMentions(true))
                .WithOverride("%suggest.number%", () => sugnum1.ToString())
                .WithOverride("%suggest.user.name%", () => user.Username)
                .WithOverride("%suggest.user.avatar%", () => user.RealAvatarUrl().ToString()).Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(await GetSuggestionMessage(guild)), guild.Id, out var embed,
                out var plainText, out _);
            var chan = await guild.GetTextChannelAsync(await GetSuggestionChannel(guild.Id)).ConfigureAwait(false);
            IUserMessage msg;
            if (!ebe)
            {
                if (await GetEmoteMode(guild) == 1)
                    msg = await chan.SendMessageAsync(replacer.Replace(await GetSuggestionMessage(guild)),
                        components: builder.Build()).ConfigureAwait(false);
                else
                    msg = await chan.SendMessageAsync(replacer.Replace(await GetSuggestionMessage(guild)))
                        .ConfigureAwait(false);
            }
            else
            {
                if (await GetEmoteMode(guild) == 1)
                    msg = await chan.SendMessageAsync(plainText, embeds: embed, components: builder.Build())
                        .ConfigureAwait(false);
                else
                    msg = await chan.SendMessageAsync(plainText, embeds: embed).ConfigureAwait(false);
            }

            if (await GetEmoteMode(guild) == 0)
            {
                if (em is null or "disabled" or "-")
                {
                    foreach (var i in reacts)
                        await msg.AddReactionAsync(i).ConfigureAwait(false);
                }
                else
                {
                    foreach (var ei in emotes)
                        await msg.AddReactionAsync(ei).ConfigureAwait(false);
                }
            }

            await Sugnum(guild, sugnum1 + 1).ConfigureAwait(false);
            await Suggest(guild, sugnum1, msg.Id, user.Id, suggestion).ConfigureAwait(false);

            if (interaction is not null)
                await interaction.SendEphemeralConfirmAsync("Suggestion has been sent!").ConfigureAwait(false);
            else
                await channel.SendConfirmAsync("Suggestion sent!").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Records a new suggestion in the database.
    /// </summary>
    /// <param name="guild">The guild where the suggestion is made.</param>
    /// <param name="suggestId">The suggestion number within the guild.</param>
    /// <param name="messageId">The ID of the message containing the suggestion.</param>
    /// <param name="userId">The ID of the user who made the suggestion.</param>
    /// <param name="suggestion">The content of the suggestion.</param>
    /// <returns>A Task representing the asynchronous operation to save the suggestion.</returns>
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
            var uow = db.GetDbContext();
            await using var _ = uow.ConfigureAwait(false);
            uow.Suggestions.Add(suggest);

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// Retrieves suggestions by guild and suggestion ID, primarily for operations involving a specific suggestion.
    /// </summary>
    /// <param name="gid">The guild ID where the suggestion was made.</param>
    /// <param name="sid">The suggestion ID to retrieve.</param>
    /// <returns>An array of <see cref="SuggestionsModel"/> matching the criteria or null if not found.</returns>
    public async Task<SuggestionsModel[]?> Suggestions(ulong gid, ulong sid)
    {
        await using var uow = db.GetDbContext();
        return await uow.Suggestions.ForId(gid, sid);
    }

    /// <summary>
    /// Lists all suggestions made in a guild, useful for overview or management purposes.
    /// </summary>
    /// <param name="gid">The guild ID to retrieve suggestions for.</param>
    /// <returns>A list of all <see cref="SuggestionsModel"/> in the specified guild.</returns>
    public List<SuggestionsModel> Suggestions(ulong gid)
    {
        using var uow = db.GetDbContext();
        return uow.Suggestions.Where(x => x.GuildId == gid).ToList();
    }

    /// <summary>
    /// Retrieves a suggestion based on its associated message ID, useful for operations triggered by message interactions.
    /// </summary>
    /// <param name="msgId">The message ID linked to the suggestion.</param>
    /// <returns>The <see cref="SuggestionsModel"/> associated with the message ID.</returns>
    public async Task<SuggestionsModel> GetSuggestByMessage(ulong msgId)
    {
        await using var uow = db.GetDbContext();
        return await uow.Suggestions.FirstOrDefaultAsyncEF(x => x.MessageId == msgId);
    }

    /// <summary>
    /// Retrieves all suggestions made by a specific user in a guild, useful for personal suggestion tracking or management.
    /// </summary>
    /// <param name="guildId">The guild ID where the suggestions were made.</param>
    /// <param name="userId">The user ID to retrieve suggestions for.</param>
    /// <returns>An array of <see cref="SuggestionsModel"/> made by the specified user in the specified guild.</returns>
    public async Task<SuggestionsModel[]> ForUser(ulong guildId, ulong userId)
    {
        await using var uow = db.GetDbContext();
        return await uow.Suggestions.ForUser(guildId, userId);
    }

    /// <summary>
    /// Determines the emote chosen by a user for a specific suggestion, useful for tallying reactions or votes.
    /// </summary>
    /// <param name="messageId">The message ID of the suggestion being voted on.</param>
    /// <param name="userId">The user ID of the voter.</param>
    /// <returns>The ID of the emote chosen by the user, or 0 if no vote was found.</returns>
    public async Task<int> GetPickedEmote(ulong messageId, ulong userId)
    {
        await using var uow = db.GetDbContext();
        var toreturn = uow.SuggestVotes.FirstOrDefault(x => x.UserId == userId && x.MessageId == messageId);
        return toreturn?.EmotePicked ?? 0;
    }

    /// <summary>
    /// Updates the emote picked by a user for a specific suggestion message. If the user has not previously
    /// picked an emote for this message, a new vote record is created. Otherwise, the existing vote is updated.
    /// </summary>
    /// <param name="messageId">The ID of the message the vote is associated with.</param>
    /// <param name="userId">The ID of the user casting or changing their vote.</param>
    /// <param name="emotePicked">The ID of the emote picked by the user.</param>
    /// <returns>A Task representing the asynchronous operation of updating or adding the vote.</returns>
    public async Task UpdatePickedEmote(ulong messageId, ulong userId, int emotePicked)
    {
        await using var uow = db.GetDbContext();
        var tocheck = uow.SuggestVotes.FirstOrDefault(x => x.MessageId == messageId && x.UserId == userId);
        if (tocheck is null)
        {
            var toadd = new SuggestVotes
            {
                EmotePicked = emotePicked, MessageId = messageId, UserId = userId
            };
            uow.SuggestVotes.Add(toadd);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            tocheck.EmotePicked = emotePicked;
            uow.SuggestVotes.Update(tocheck);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds a new thread channel association with a suggestion message. This is used to track thread channels
    /// that are created for discussing specific suggestions.
    /// </summary>
    /// <param name="messageId">The message ID of the suggestion.</param>
    /// <param name="threadChannelId">The thread channel ID created for the suggestion's discussion.</param>
    /// <returns>A Task representing the asynchronous operation of adding the thread channel association.</returns>
    public async Task AddThreadChannel(ulong messageId, ulong threadChannelId)
    {
        await using var uow = db.GetDbContext();
        uow.SuggestThreads.Add(new SuggestThreads
        {
            MessageId = messageId, ThreadChannelId = threadChannelId
        });
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the ID of the thread channel associated with a specific suggestion message. This is used to
    /// find the discussion thread for a suggestion.
    /// </summary>
    /// <param name="messageId">The message ID of the suggestion.</param>
    /// <returns>The ID of the thread channel associated with the message, or 0 if no association exists.</returns>
    public async Task<ulong> GetThreadByMessage(ulong messageId)
    {
        await using var uow = db.GetDbContext();
        return uow.SuggestThreads.FirstOrDefault(x => x.MessageId == messageId)?.ThreadChannelId ?? 0;
    }
}