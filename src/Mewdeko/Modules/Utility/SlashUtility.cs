using System.IO;
using Discord.Interactions;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.JsonSettings;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Utility;

/// <summary>
/// Contains utility commands such as user and server information retrieval.
/// </summary>
[Group("utility", "Utility commands like userinfo")]
public class SlashUtility(
    DiscordSocketClient client,
    ICoordinator coordinator,
    StatsService stats,
    IBotCredentials creds,
    MuteService muteService,
    BotConfigService config,
    DbService db) : MewdekoSlashModuleBase<UtilityService>
{
    /// <summary>
    /// Displays the avatar of a user. This can either be their global Discord avatar or their server-specific avatar if available.
    /// </summary>
    /// <param name="avType">The type of avatar to display ("real" for global, "guild" for server-specific).</param>
    /// <param name="userId">The ID of the user whose avatar is being requested.</param>
    [ComponentInteraction("avatartype:*,*", true), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    public async Task Avatar(string avType, ulong userId)
    {
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var user = await client.Rest.GetGuildUserAsync(ctx.Guild.Id, userId);
        switch (avType)
        {
            case "real":
                var avatarUrl = user.AvatarId.StartsWith("a_", StringComparison.InvariantCulture)
                    ? $"{DiscordConfig.CDNUrl}avatars/{user.Id}/{user.AvatarId}.gif?size=2048"
                    : $"{DiscordConfig.CDNUrl}avatars/{user.Id}/{user.AvatarId}.png?size=2048";
                var componentbuilder = new ComponentBuilder().WithButton("Guild Avatar", $"avatartype:guild,{userId}");
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .AddField(efb => efb.WithName("Username").WithValue(user.ToString()).WithIsInline(true))
                    .AddField(efb =>
                        efb.WithName("Real Avatar Url").WithValue($"[Link]({avatarUrl})").WithIsInline(true))
                    .WithImageUrl(avatarUrl);
                await componentInteraction.UpdateAsync(x =>
                {
                    x.Embed = eb.Build();
                    x.Components = componentbuilder.Build();
                });
                break;
            case "guild":
                var avatarUrlGuild = user.GuildAvatarId.StartsWith("a_", StringComparison.InvariantCulture)
                    ? $"{DiscordConfig.CDNUrl}guilds/{ctx.Guild.Id}/users/{user.Id}/avatars/{user.GuildAvatarId}.gif?size=2048"
                    : $"{DiscordConfig.CDNUrl}guilds/{ctx.Guild.Id}/users/{user.Id}/avatars/{user.GuildAvatarId}.png?size=2048";
                var componentbuilderGuild =
                    new ComponentBuilder().WithButton("Real Avatar", $"avatartype:real,{userId}");
                var ebGuild = new EmbedBuilder()
                    .WithOkColor()
                    .AddField(efb => efb.WithName("Username").WithValue(user.ToString()).WithIsInline(true))
                    .AddField(efb =>
                        efb.WithName("Guild Avatar Url").WithValue($"[Link]({avatarUrlGuild})").WithIsInline(true))
                    .WithImageUrl(avatarUrlGuild);
                await componentInteraction.UpdateAsync(x =>
                {
                    x.Embed = ebGuild.Build();
                    x.Components = componentbuilderGuild.Build();
                });
                break;
        }
    }

    /// <summary>
    /// Toggles between displaying a user's real banner and their guild-specific banner.
    /// </summary>
    /// <param name="avType">Specifies the type of banner to display. "real" for the user's global Discord banner, "guild" for the guild-specific banner.</param>
    /// <param name="userId">The ID of the user whose banner is being requested.</param>
    /// <returns>A task that represents the asynchronous operation of updating the interaction with the requested banner information.</returns>
    /// <remarks>This method allows users to interactively switch between viewing a user's global banner and a guild-specific banner if available.</remarks>
    [ComponentInteraction("avatartype:*,*", true), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    public async Task Banner(string avType, ulong userId)
    {
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var guildUser = await client.Rest.GetGuildUserAsync(ctx.Guild.Id, userId);
        var user = await client.Rest.GetUserAsync(userId);
        switch (avType)
        {
            case "real":
                var avatarUrl = user.GetAvatarUrl(size: 2048);
                var componentbuilder = new ComponentBuilder().WithButton("Guild Banner", $"bannertype:guild,{userId}");
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .AddField(efb => efb.WithName("Username").WithValue(user.ToString()).WithIsInline(true))
                    .AddField(efb =>
                        efb.WithName("Real Banneer Url").WithValue($"[Link]({avatarUrl})").WithIsInline(true))
                    .WithImageUrl(avatarUrl);
                await componentInteraction.UpdateAsync(x =>
                {
                    x.Embed = eb.Build();
                    x.Components = componentbuilder.Build();
                });
                break;
            case "guild":
                var avatarUrlGuild = guildUser.GetBannerUrl(size: 2048);
                var componentbuilderGuild =
                    new ComponentBuilder().WithButton("Real Banner", $"bannertype:real,{userId}");
                var ebGuild = new EmbedBuilder()
                    .WithOkColor()
                    .AddField(efb => efb.WithName("Username").WithValue(user.ToString()).WithIsInline(true))
                    .AddField(efb =>
                        efb.WithName("Guild Banner Url").WithValue($"[Link]({avatarUrlGuild})").WithIsInline(true))
                    .WithImageUrl(avatarUrlGuild);
                await componentInteraction.UpdateAsync(x =>
                {
                    x.Embed = ebGuild.Build();
                    x.Components = componentbuilderGuild.Build();
                });
                break;
        }
    }


    /// <summary>
    /// Retrieves the JSON representation of a specified message. This can be used for debugging or as a template for constructing custom embeds.
    /// </summary>
    /// <param name="messageId">The ID of the message to convert to JSON format.</param>
    /// <param name="channel">The channel from which the message will be fetched. If not specified, uses the current channel.</param>
    /// <returns>A task that represents the asynchronous operation of sending the message's JSON representation to the requester.</returns>
    [SlashCommand("getjson", "Gets the json from a message to use with our embed builder!"),
     RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task GetJson(ulong messageId, ITextChannel channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new LowercaseContractResolver(), NullValueHandling = NullValueHandling.Ignore
        };

        var message = await channel.GetMessageAsync(messageId);
        var serialized = JsonConvert.SerializeObject(message.GetNewEmbedSource(), Formatting.Indented, settings);
        using var ms = new MemoryStream();
        await using var writer = new StreamWriter(ms);
        await writer.WriteAsync(serialized);
        await writer.FlushAsync();
        ms.Position = 0;
        await ctx.Interaction.RespondWithFileAsync(ms, "EmbedJson.txt");
        await ms.DisposeAsync();
        await writer.DisposeAsync();
    }

    /// <summary>
    /// Prompts the user with a modal to send a custom message to a specified channel. This can include text and embeds.
    /// </summary>
    /// <param name="channel">The channel to which the message will be sent. If not specified, uses the current channel.</param>
    /// <returns>A task that represents the asynchronous operation of responding with a modal for message input.</returns>
    [SlashCommand("say", "Send a message to a channel or the current channel"), CheckPermissions,
     SlashUserPerm(ChannelPermission.ManageMessages)]
    public Task Say(
        [Summary("SendTo", "The channel to send to. Defaults to the current channel.")]
        ITextChannel channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        return RespondWithModalAsync<SayModal>($"saymodal:{channel.Id}");
    }

    /// <summary>
    /// Processes a modal submission for sending a message. Allows sending messages with text, embeds, and components.
    /// </summary>
    /// <param name="channelId">The ID of the channel where the message will be sent.</param>
    /// <param name="modal">The modal containing the message to be sent.</param>
    /// <returns>A task that represents the asynchronous operation of sending a message and confirming its delivery.</returns>
    /// <remarks>This method handles modal submissions from the "say" command, processing user input and sending the resulting message to the specified channel.</remarks>
    [ModalInteraction("saymodal:*", ignoreGroupNames: true)]
    public async Task SayModal(ulong channelId, SayModal modal)
    {
        var channel = await ctx.Guild.GetTextChannelAsync(channelId);
        var canMention = ((IGuildUser)ctx.User).GuildPermissions.MentionEveryone;
        var rep = new ReplacementBuilder()
            .WithDefault(ctx.User, channel, (SocketGuild)ctx.Guild, (DiscordSocketClient)ctx.Client).Build();

        if (SmartEmbed.TryParse(rep.Replace(modal.Message), ctx.Guild?.Id, out var embedData, out var plainText,
                out var components))
        {
            await channel.SendMessageAsync(plainText, embeds: embedData, components: components?.Build(),
                    allowedMentions: !canMention ? new AllowedMentions(AllowedMentionTypes.Users) : AllowedMentions.All)
                .ConfigureAwait(false);
            await ctx.Interaction.SendEphemeralConfirmAsync($"Message sent to {channel.Mention}.");
        }
        else
        {
            var msg = rep.Replace(modal.Message);
            if (!string.IsNullOrWhiteSpace(msg))
            {
                await channel.SendConfirmAsync(msg).ConfigureAwait(false);
                await ctx.Interaction.SendEphemeralConfirmAsync($"Message sent to {channel.Mention}.");
            }
            else
                await ctx.Interaction.SendEphemeralErrorAsync(
                    "The message was empty after variable replacements. Please double check your input.", Config);
        }
    }

    /// <summary>
    /// Displays the bot's current statistics, including memory usage, uptime, and command usage rates.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of displaying bot statistics.</returns>
    [SlashCommand("stats", "Shows the bots current stats"), CheckPermissions,
     SlashUserPerm(GuildPermission.SendMessages)]
    public async Task Stats()
    {
        await using var uow = db.GetDbContext();
        var time = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(5));
        var commandStats = uow.CommandStats.Count(x => x.DateAdded.Value >= time);
        var users = new[]
        {
            await client.Rest.GetUserAsync(280835732728184843).ConfigureAwait(false),
            await client.Rest.GetUserAsync(786375627892064257).ConfigureAwait(false),
        };
        await ctx.Interaction.RespondAsync(embed:
                new EmbedBuilder().WithOkColor()
                    .WithAuthor($"{client.CurrentUser.Username} v{StatsService.BotVersion}",
                        client.CurrentUser.GetAvatarUrl(), config.Data.SupportServer)
                    .AddField(GetText("authors"),
                        $"[{users[0]}](https://github.com/SylveonDeko)\n[{users[1]}](https://github.com/CottageDwellingCat)")
                    .AddField(GetText("commands_ran"), $"{commandStats}/5s")
                    .AddField("Library", stats.Library)
                    .AddField(GetText("owner_ids"), string.Join("\n", creds.OwnerIds.Select(x => $"<@{x}>")))
                    .AddField(GetText("shard"), $"#{client.ShardId} / {creds.TotalShards}")
                    .AddField(GetText("memory"), $"{stats.Heap} MB")
                    .AddField(GetText("uptime"), stats.GetUptimeString("\n"))
                    .AddField("Servers", $"{coordinator.GetGuildCount()} Servers").Build())
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Provides detailed information about a specific role within the server, including the number of users with the role and its permissions.
    /// </summary>
    /// <param name="role">The role to gather information about.</param>
    /// <returns>A task that represents the asynchronous operation of displaying role information.</returns>
    [SlashCommand("roleinfo", "Shows info for a role")]
    public async Task RInfo(IRole role)
    {
        var eb = new EmbedBuilder().WithTitle(role.Name).AddField("Users in role",
                (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => x.RoleIds.Contains(role.Id)))
            .AddField("Is Mentionable", role.IsMentionable).AddField("Is Hoisted", role.IsHoisted)
            .AddField("Color", role.Color.RawValue)
            .AddField("Is Managed", role.IsManaged).AddField("Permissions", string.Join(",", role.Permissions))
            .AddField("Creation Date", TimestampTag.FromDateTimeOffset(role.CreatedAt))
            .AddField("Position", role.Position).AddField("ID", role.Id)
            .WithThumbnailUrl(role.GetIconUrl()).WithColor(role.Color);

        await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Displays information about a voice channel, including the number of users in the channel, its creation date, bitrate, and user limit.
    /// </summary>
    /// <param name="channel">The voice channel to display information about. If null, attempts to use the voice channel the user is currently in.</param>
    /// <returns>A task that represents the asynchronous operation of sending an embed with voice channel information.</returns>
    [SlashCommand("voiceinfo", "Shows info for a voice channel"), CheckPermissions,
     SlashUserPerm(GuildPermission.SendMessages)]
    public async Task VInfo(IVoiceChannel channel = null)
    {
        var voiceChannel = ((IGuildUser)ctx.User).VoiceChannel;
        var eb = new EmbedBuilder();
        switch (voiceChannel)
        {
            case null when channel == null:
                await ctx.Interaction
                    .SendEphemeralErrorAsync(
                        "You arent in a voice channel, and you haven't mentioned one either to use this command!",
                        Config)
                    .ConfigureAwait(false);
                return;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            case null when channel is not null:
                eb.WithTitle(channel.Name);
                eb.AddField("Users", (await channel.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count());
                eb.AddField("Created On", channel.CreatedAt);
                eb.AddField("Bitrate", channel.Bitrate);
                eb.AddField("User Limit", channel.UserLimit == null ? "Infinite" : channel.UserLimit);
                eb.AddField("Channel ID", channel.Id);
                eb.WithOkColor();
                await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
                break;
        }

        if (voiceChannel is not null && channel is not null)
        {
            eb.WithTitle(channel.Name);
            eb.AddField("Users", (await channel.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count());
            eb.AddField("Created On", channel.CreatedAt);
            eb.AddField("Bitrate", channel.Bitrate);
            eb.AddField("User Limit", channel.UserLimit == null ? "Infinite" : channel.UserLimit);
            eb.AddField("Channel ID", channel.Id);
            eb.WithOkColor();
            await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        if (voiceChannel is not null && channel is null)
        {
            eb.WithTitle(voiceChannel.Name);
            eb.AddField("Users", (await voiceChannel.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count());
            eb.AddField("Created On", voiceChannel.CreatedAt);
            eb.AddField("Bitrate", voiceChannel.Bitrate);
            eb.AddField("User Limit", voiceChannel.UserLimit == null ? "Infinite" : voiceChannel.UserLimit);
            eb.AddField("Channel ID", voiceChannel.Id);
            eb.WithOkColor();
            await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retrieves and displays detailed information about a user, whether they are part of the server or not, using their Discord ID.
    /// </summary>
    /// <param name="userIdstring">The user ID as a string. Must be convertible to an ulong.</param>
    /// <returns>A task that represents the asynchronous operation of displaying fetched user information.</returns>
    [SlashCommand("fetch", "Gets a user, even if they aren't in the server."), CheckPermissions,
     SlashUserPerm(GuildPermission.SendMessages)]
    public async Task Fetch(
        [Summary("userId", "The user's ID. Looks like this: 280835732728184843")]
        string userIdstring)
    {
        // Because discord is ass and uses int32 instead of int64
        if (!ulong.TryParse(userIdstring, out var userId))
        {
            await ctx.Interaction.SendEphemeralErrorAsync("Please make sure that you put an ID in.", Config);
            return;
        }

        var usr = await client.Rest.GetUserAsync(userId).ConfigureAwait(false);
        if (usr is null)
        {
            await ctx.Interaction.SendErrorAsync(
                "That user could not be found. Please ensure that was the correct ID.", Config);
        }
        else
        {
            var embed = new EmbedBuilder().WithTitle("info for fetched user").AddField("Username", usr)
                .AddField("Created At", TimestampTag.FromDateTimeOffset(usr.CreatedAt))
                .AddField("Public Flags", usr.PublicFlags).WithImageUrl(usr.RealAvatarUrl().ToString()).WithOkColor();
            await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Provides comprehensive information about the server, including the total number of users, channels, roles, and custom emojis.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of sending an embed with server information.</returns>
    [SlashCommand("serverinfo", "Shows info for this server."), CheckPermissions,
     SlashUserPerm(GuildPermission.SendMessages)]
    public async Task ServerInfo()
    {
        await DeferAsync();
        if (ctx.Guild is not SocketGuild guild)
            return;
        var ownername = guild.GetUser(guild.OwnerId);
        var textchn = guild.TextChannels.Count;
        var voicechn = guild.VoiceChannels.Count;

        var component = new ComponentBuilder().WithButton("More Info", "moreinfo");
        var embed = new EmbedBuilder().WithAuthor(eab => eab.WithName(GetText("server_info"))).WithTitle(guild.Name)
            .AddField("Id", guild.Id.ToString())
            .AddField("Owner", ownername.Mention).AddField("Total Users", guild.Users.Count.ToString())
            .AddField("Created On", TimestampTag.FromDateTimeOffset(guild.CreatedAt)).WithColor(Mewdeko.OkColor);
        if (guild.SplashUrl != null)
            embed.WithImageUrl($"{guild.SplashUrl}?size=2048");
        if (Uri.IsWellFormedUriString(guild.IconUrl, UriKind.Absolute))
            embed.WithThumbnailUrl(guild.IconUrl);
        if (guild.Emotes.Count > 0)
        {
            embed.AddField(fb =>
                fb.WithName($"{GetText("custom_emojis")}({guild.Emotes.Count})")
                    .WithValue(string.Join(" ", guild.Emotes.Shuffle().Take(30).Select(e => $"{e}")).TrimTo(1024)));
        }

        var msg = await ctx.Interaction.FollowupAsync(embed: embed.Build(), components: component.Build())
            .ConfigureAwait(false);
        var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
        if (input == "moreinfo")
        {
            var vals = Enum.GetValues(typeof(GuildFeature)).Cast<GuildFeature>();
            var setFeatures = vals.Where(x => guild.Features.Value.HasFlag(x));
            embed.AddField("Bots", (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => x.IsBot))
                .AddField("Users", (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => !x.IsBot))
                .AddField("Text Channels", textchn.ToString())
                .AddField("Voice Channels", voicechn.ToString()).AddField("Roles", (guild.Roles.Count - 1).ToString())
                .AddField("Server Features", Format.Code(string.Join("\n", setFeatures)));
            await msg.ModifyAsync(x =>
            {
                x.Embed = embed.Build();
                x.Components = null;
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Displays detailed information about a text channel, such as its ID, creation date, number of users, NSFW status, and more.
    /// </summary>
    /// <param name="channel">The text channel to display information about. If null, uses the current channel.</param>
    /// <returns>A task that represents the asynchronous operation of sending an embed with channel information.</returns>
    [SlashCommand("channelinfo", "Shows info for the current or mentioned channel"), CheckPermissions,
     SlashUserPerm(GuildPermission.SendMessages)]
    public async Task ChannelInfo(ITextChannel channel = null)
    {
        var ch = channel ?? (ITextChannel)ctx.Channel;
        var embed = new EmbedBuilder().WithTitle(ch.Name).AddField(GetText("id"), ch.Id.ToString())
            .AddField(GetText("created_at"), TimestampTag.FromDateTimeOffset(ch.CreatedAt))
            .AddField(GetText("users"), (await ch.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count())
            .AddField("NSFW", ch.IsNsfw)
            .AddField("Slowmode Interval", TimeSpan.FromSeconds(ch.SlowModeInterval).Humanize())
            .AddField("Default Thread Archive Duration", ch.DefaultArchiveDuration).WithColor(Mewdeko.OkColor);
        if (!string.IsNullOrWhiteSpace(ch.Topic))
            embed.WithDescription(ch.Topic);
        await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows detailed information about a user, including their server roles, activities, join dates, and any server-specific attributes like nicknames.
    /// </summary>
    /// <param name="usr">The user to display information about. If null, uses the command invoker.</param>
    /// <returns>A task that represents the asynchronous operation of displaying user information.</returns>
    /// <remarks>This command also provides a button for additional, detailed information.</remarks>
    [SlashCommand("userinfo", "Shows info for a mentioned or current user"), CheckPermissions,
     SlashUserPerm(GuildPermission.SendMessages)]
    [UserCommand("User Info")]
    public async Task UserInfo(IUser usr = null)
    {
        if (ctx.Interaction is IUserCommandInteraction or IMessageCommandInteraction)
            await DeferAsync(true);
        else
            await DeferAsync();
        var component = new ComponentBuilder().WithButton("More Info", "moreinfo");
        var user = usr as IGuildUser ?? ctx.User as IGuildUser;
        var userbanner = (await client.Rest.GetUserAsync(user.Id).ConfigureAwait(false)).GetBannerUrl(size: 2048);
        var serverUserType = user.GuildPermissions.Administrator ? "Administrator" : "Regular User";
        var restUser = await client.Rest.GetUserAsync(user.Id);
        var embed = new EmbedBuilder().AddField("Username", user.ToString())
            .WithColor(restUser.AccentColor ?? Mewdeko.OkColor);

        if (!string.IsNullOrWhiteSpace(user.Nickname))
            embed.AddField("Nickname", user.Nickname);

        embed.AddField("User Id", user.Id).AddField("User Type", serverUserType).AddField("Joined Server",
                TimestampTag.FromDateTimeOffset(user.JoinedAt.GetValueOrDefault()))
            .AddField("Joined Discord", TimestampTag.FromDateTimeOffset(user.CreatedAt)).AddField("Role Count",
                user.GetRoles().Count(r => r.Id != r.Guild.EveryoneRole.Id));

        if (user.Activities.Count > 0)
        {
            embed.AddField("Activities",
                string.Join("\n", user.Activities.Select(x => string.Format($"{x.Name}: {x.Details ?? ""}"))));
        }

        var av = user.RealAvatarUrl();
        if (av.IsAbsoluteUri)
        {
            if (userbanner is not null)
            {
                embed.WithThumbnailUrl(av.ToString());
                embed.WithImageUrl(userbanner);
            }
            else
            {
                embed.WithImageUrl(av.ToString());
            }
        }

        var msg = await ctx.Interaction.FollowupAsync(embed: embed.Build(), components: component.Build())
            .ConfigureAwait(false);
        var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
        if (input == "moreinfo")
        {
            if (user.GetRoles().Any(x => x.Id != ctx.Guild.EveryoneRole.Id))
            {
                embed.AddField("Roles",
                    string.Join("", user.GetRoles().OrderBy(x => x.Position).Select(x => x.Mention)));
            }

            embed.AddField("Deafened", user.IsDeafened);
            embed.AddField("Is VC Muted", user.IsMuted);
            embed.AddField("Is Server Muted",
                user.GetRoles().Contains(await muteService.GetMuteRole(ctx.Guild).ConfigureAwait(false)));
            await ctx.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Embed = embed.Build();
                x.Components = null;
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A message command that fetches and displays information for the author of a specific message.
    /// </summary>
    /// <param name="message">The message whose author's information will be displayed.</param>
    /// <returns>A task that represents the asynchronous operation of sending user information.</returns>
    [MessageCommand("User Info"), CheckPermissions, SlashUserPerm(ChannelPermission.SendMessages)]
    public Task UserInfo(IMessage message) => UserInfo(message.Author);

    /// <summary>
    /// Retrieves and displays the ID of a specified user or the command invoker if no user is specified.
    /// </summary>
    /// <param name="user">The user whose ID is to be retrieved. If null, the command invoker's ID is used.</param>
    /// <returns>A task that represents the asynchronous operation of sending the user's ID.</returns>
    [SlashCommand("userid", "Grabs the ID of a user."), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    [UserCommand("User ID")]
    public Task UserId(IUser user = null) =>
        ctx.Interaction.RespondAsync(user?.Id.ToString() ?? ctx.User.Id.ToString(), ephemeral: true);

    /// <summary>
    /// A message command that fetches and displays the ID of the author of a specific message.
    /// </summary>
    /// <param name="message">The message whose author's ID will be displayed.</param>
    /// <returns>A task that represents the asynchronous operation of sending the message author's ID.</returns>
    [MessageCommand("User ID"), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    public Task UserId(IMessage message) => UserId(message.Author);

    /// <summary>
    /// Displays the avatar of a specified user, with an option to switch between their global Discord avatar and their server-specific avatar.
    /// </summary>
    /// <param name="usr">The user whose avatar is to be displayed. If null, the command invoker's avatar is used.</param>
    /// <returns>A task that represents the asynchronous operation of sending the avatar image.</returns>
    [SlashCommand("avatar", "Shows a user's avatar"), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    [UserCommand("Avatar")]
    public async Task Avatar(IUser usr = null)
    {
        if (ctx.Interaction is IUserCommandInteraction or IMessageCommandInteraction)
            await DeferAsync(true);
        else
            await DeferAsync();

        usr ??= (IGuildUser)ctx.User;
        var components = new ComponentBuilder().WithButton("Non-Guild Avatar", $"avatartype:real,{usr.Id}");

        var avatarUrl = usr.GetAvatarUrl(ImageFormat.Auto, 2048);

        if (avatarUrl == null)
        {
            await ReplyErrorLocalizedAsync("avatar_none", usr.ToString()).ConfigureAwait(false);
            return;
        }

        var av = await client.Rest.GetGuildUserAsync(ctx.Guild.Id, usr.Id);
        if (av.GuildAvatarId is not null)
            avatarUrl = av.GuildAvatarId.StartsWith("a_", StringComparison.InvariantCulture)
                ? $"{DiscordConfig.CDNUrl}guilds/{ctx.Guild.Id}/users/{usr.Id}/avatars/{av.GuildAvatarId}.gif?size=2048"
                : $"{DiscordConfig.CDNUrl}guilds/{ctx.Guild.Id}/users/{usr.Id}/avatars/{av.GuildAvatarId}.png?size=2048";

        await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .AddField(efb => efb.WithName("Username").WithValue(usr.ToString()).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName($"{(av.GuildAvatarId is null ? "" : "Guild")} Avatar Url")
                        .WithValue($"[Link]({avatarUrl})").WithIsInline(true))
                .WithImageUrl(avatarUrl).Build(), components: av.GuildAvatarId is null ? null : components.Build())
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Converts a local time to a universal Discord timestamp, allowing it to be displayed in the user's local timezone when viewed on Discord.
    /// </summary>
    /// <param name="tz">The timezone ID for the time conversion.</param>
    /// <param name="time">The local time to be converted to a timestamp.</param>
    /// <param name="format">The format of the timestamp to be generated.</param>
    /// <returns>A task that represents the asynchronous operation of sending the timestamp.</returns>
    [SlashCommand("timestamp", "Converts your local time to a universal timestamp")]
    public Task GenerateTimestamp
    (
        [Autocomplete(typeof(TimeZoneAutocompleter)), Summary("timezone", "your timezone")]
        string tz,
        [Summary("time", "the time you want to generate a timestamp for")]
        DateTime time,
        [Summary("format", "How should the timestamp be formatted")]
        TimestampTagStyles format = TimestampTagStyles.ShortDateTime
    )
    {
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(tz);
        var utc = TimeZoneInfo.ConvertTimeToUtc(time, timezone);
        var tag = TimestampTag.FromDateTimeOffset(utc, format);
        return ctx.Interaction.SendEphemeralConfirmAsync($"{tag} (`{tag}`)");
    }

    /// <summary>
    /// Provides additional server information on request via a component interaction, including counts of bots, users, channels, roles, and server features.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of updating the original interaction response with more detailed server information.</returns>
    [ComponentInteraction("moresinfo", true)]
    public async Task MoreSInfo()
    {
        var textchn = (await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count;
        var voicechn = (await ctx.Guild.GetVoiceChannelsAsync().ConfigureAwait(false)).Count;
        await DeferAsync();
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var embed = componentInteraction.Message.Embeds.FirstOrDefault().ToEmbedBuilder();
        var vals = Enum.GetValues(typeof(GuildFeature)).Cast<GuildFeature>();
        var setFeatures = vals.Where(x => Context.Guild.Features.Value.HasFlag(x));
        embed
            .AddField("Bots", (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => x.IsBot))
            .AddField("Users", (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => !x.IsBot))
            .AddField("Text Channels", textchn.ToString())
            .AddField("Voice Channels", voicechn.ToString())
            .AddField("Roles", ctx.Guild.Roles.Count.ToString())
            .AddField("Server Features", Format.Code(string.Join("\n", setFeatures)));
        await componentInteraction.Message.ModifyAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = null;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides additional user information via a component interaction, including roles, voice channel states, and mute status within the server.
    /// </summary>
    /// <param name="userId">The ID of the user for whom additional information is being requested.</param>
    /// <returns>A task that represents the asynchronous operation of updating the original interaction response with detailed user information.</returns>
    /// <remarks>This method is triggered by an interactive component, allowing users to request more detailed information about a specific user within the server.</remarks>
    [ComponentInteraction("moreuinfo:*", true)]
    public async Task MoreUInfo(ulong userId)
    {
        await DeferAsync();
        var user = await ctx.Guild.GetUserAsync(userId).ConfigureAwait(false);
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var embed = componentInteraction.Message.Embeds.FirstOrDefault().ToEmbedBuilder();
        if (user.GetRoles().Any(x => x.Id != ctx.Guild.EveryoneRole.Id))
        {
            embed.AddField("Roles",
                string.Join("", user.GetRoles().OrderBy(x => x.Position).Select(x => x.Mention)));
        }

        embed.AddField("Deafened", user.IsDeafened);
        embed.AddField("Is VC Muted", user.IsMuted);
        embed.AddField("Is Server Muted",
            user.GetRoles().Contains(await muteService.GetMuteRole(ctx.Guild).ConfigureAwait(false)));
        await componentInteraction.Message.ModifyAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = null;
        }).ConfigureAwait(false);
    }
}