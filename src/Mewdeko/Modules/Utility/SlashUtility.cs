using System.Threading.Tasks;
using Discord.Interactions;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Utility;

[Group("utility", "Utility commands like userinfo")]
public class SlashUtility : MewdekoSlashModuleBase<UtilityService>
{
    private readonly DiscordSocketClient client;
    private readonly ICoordinator coordinator;
    private readonly StatsService stats;
    private readonly IBotCredentials creds;
    private readonly MuteService muteService;
    private readonly BotConfigService config;
    private readonly DbService db;

    public SlashUtility(
        DiscordSocketClient client,
        ICoordinator coord,
        StatsService stats,
        IBotCredentials credentials,
        MuteService muteService, BotConfigService config, DbService db)
    {
        this.client = client;
        coordinator = coord;
        this.stats = stats;
        creds = credentials;
        this.muteService = muteService;
        this.config = config;
        this.db = db;
    }

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
                var componentbuilderGuild = new ComponentBuilder().WithButton("Real Avatar", $"avatartype:real,{userId}");
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

    [SlashCommand("say", "Send a message to a channel or the current channel"), CheckPermissions, SlashUserPerm(ChannelPermission.ManageMessages)]
    public async Task Say([Summary("SendTo", "The channel to send to. Defaults to the current channel.")] ITextChannel channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        await RespondWithModalAsync<SayModal>($"saymodal:{channel.Id}");
    }

    [ModalInteraction("saymodal:*", ignoreGroupNames: true)]
    public async Task SayModal(ulong channelId, SayModal modal)
    {
        var channel = await ctx.Guild.GetTextChannelAsync(channelId);
        var canMention = ((IGuildUser)ctx.User).GuildPermissions.MentionEveryone;
        var rep = new ReplacementBuilder().WithDefault(ctx.User, channel, (SocketGuild)ctx.Guild, (DiscordSocketClient)ctx.Client).Build();

        if (SmartEmbed.TryParse(rep.Replace(modal.Message), ctx.Guild?.Id, out var embedData, out var plainText, out var components))
        {
            await channel.SendMessageAsync(plainText, embeds: embedData, components: components?.Build(),
                allowedMentions: !canMention ? new AllowedMentions(AllowedMentionTypes.Users) : AllowedMentions.All).ConfigureAwait(false);
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
                await ctx.Interaction.SendEphemeralErrorAsync("The message was emoty after variable replacements. Please double check your input.");
        }
    }

    [SlashCommand("stats", "Shows the bots current stats"), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    public async Task Stats()
    {
        await using var uow = db.GetDbContext();
        var time = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(5));
        var commandStats = uow.CommandStats.Count(x => x.DateAdded.Value >= time);
        var user = await client.Rest.GetUserAsync(280835732728184843).ConfigureAwait(false);
        await ctx.Interaction.RespondAsync(embed:
                new EmbedBuilder().WithOkColor()
                    .WithAuthor($"{client.CurrentUser.Username} v{StatsService.BotVersion}", client.CurrentUser.GetAvatarUrl(), config.Data.SupportServer)
                    .AddField(GetText("author"), $"{user.Mention} | {user.Username}#{user.Discriminator}")
                    .AddField(GetText("commands_ran"), $"{commandStats}/5s")
                    .AddField("Library", stats.Library)
                    .AddField(GetText("owner_ids"), string.Join("\n", creds.OwnerIds.Select(x => $"<@{x}>")))
                    .AddField(GetText("shard"), $"#{client.ShardId} / {creds.TotalShards}")
                    .AddField(GetText("memory"), $"{stats.Heap} MB")
                    .AddField(GetText("uptime"), stats.GetUptimeString("\n"))
                    .AddField("Servers", $"{coordinator.GetGuildCount()} Servers").Build())
            .ConfigureAwait(false);
    }

    [SlashCommand("roleinfo", "Shows info for a role")]
    public async Task RInfo(IRole role)
    {
        var eb = new EmbedBuilder().WithTitle(role.Name).AddField("Users in role", (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => x.RoleIds.Contains(role.Id)))
            .AddField("Is Mentionable", role.IsMentionable).AddField("Is Hoisted", role.IsHoisted).AddField("Color", role.Color.RawValue)
            .AddField("Is Managed", role.IsManaged).AddField("Permissions", string.Join(",", role.Permissions))
            .AddField("Creation Date", TimestampTag.FromDateTimeOffset(role.CreatedAt)).AddField("Position", role.Position).AddField("ID", role.Id)
            .WithThumbnailUrl(role.GetIconUrl()).WithColor(role.Color);

        await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    [SlashCommand("voiceinfo", "Shows info for a voice channel"), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    public async Task VInfo(IVoiceChannel channel = null)
    {
        var voiceChannel = ((IGuildUser)ctx.User).VoiceChannel;
        var eb = new EmbedBuilder();
        switch (voiceChannel)
        {
            case null when channel == null:
                await ctx.Interaction.SendEphemeralErrorAsync("You arent in a voice channel, and you haven't mentioned one either to use this command!").ConfigureAwait(false);
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

    [SlashCommand("fetch", "Gets a user, even if they aren't in the server."), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    public async Task Fetch([Summary("userId", "The user's ID. Looks like this: 280835732728184843")] string userIdstring)
    {
        // Because discord is ass and uses int32 instead of int64
        if (!ulong.TryParse(userIdstring, out var userId))
        {
            await ctx.Interaction.SendEphemeralErrorAsync("Please make sure that you put an ID in.");
            return;
        }

        var usr = await client.Rest.GetUserAsync(userId).ConfigureAwait(false);
        if (usr is null)
        {
            await ctx.Interaction.SendErrorAsync("That user could not be found. Please ensure that was the correct ID.");
        }
        else
        {
            var embed = new EmbedBuilder().WithTitle("info for fetched user").AddField("Username", usr).AddField("Created At", TimestampTag.FromDateTimeOffset(usr.CreatedAt))
                .AddField("Public Flags", usr.PublicFlags).WithImageUrl(usr.RealAvatarUrl().ToString()).WithOkColor();
            await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
        }
    }

    [SlashCommand("serverinfo", "Shows info for this server."), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    public async Task ServerInfo()
    {
        await DeferAsync();
        if (ctx.Guild is not SocketGuild guild)
            return;
        var ownername = guild.GetUser(guild.OwnerId);
        var textchn = guild.TextChannels.Count;
        var voicechn = guild.VoiceChannels.Count;

        var component = new ComponentBuilder().WithButton("More Info", "moreinfo");
        var embed = new EmbedBuilder().WithAuthor(eab => eab.WithName(GetText("server_info"))).WithTitle(guild.Name).AddField("Id", guild.Id.ToString())
            .AddField("Owner", ownername.Mention).AddField("Total Users", guild.Users.Count.ToString())
            .AddField("Created On", TimestampTag.FromDateTimeOffset(guild.CreatedAt)).WithColor(Mewdeko.OkColor);
        if (guild.SplashUrl != null)
            embed.WithImageUrl($"{guild.SplashUrl}?size=2048");
        if (Uri.IsWellFormedUriString(guild.IconUrl, UriKind.Absolute))
            embed.WithThumbnailUrl(guild.IconUrl);
        if (guild.Emotes.Count > 0)
        {
            embed.AddField(fb =>
                fb.WithName($"{GetText("custom_emojis")}({guild.Emotes.Count})").WithValue(string.Join(" ", guild.Emotes.Shuffle().Take(30).Select(e => $"{e}")).TrimTo(1024)));
        }

        var msg = await ctx.Interaction.FollowupAsync(embed: embed.Build(), components: component.Build()).ConfigureAwait(false);
        var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
        if (input == "moreinfo")
        {
            var vals = Enum.GetValues(typeof(GuildFeature)).Cast<GuildFeature>();
            var setFeatures = vals.Where(x => guild.Features.Value.HasFlag(x));
            embed.AddField("Bots", (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => x.IsBot))
                .AddField("Users", (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => !x.IsBot)).AddField("Text Channels", textchn.ToString())
                .AddField("Voice Channels", voicechn.ToString()).AddField("Roles", (guild.Roles.Count - 1).ToString())
                .AddField("Server Features", Format.Code(string.Join("\n", setFeatures)));
            await msg.ModifyAsync(x =>
            {
                x.Embed = embed.Build();
                x.Components = null;
            }).ConfigureAwait(false);
        }
    }

    [SlashCommand("channelinfo", "Shows info for the current or mentioned channel"), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    public async Task ChannelInfo(ITextChannel channel = null)
    {
        var ch = channel ?? (ITextChannel)ctx.Channel;
        var embed = new EmbedBuilder().WithTitle(ch.Name).AddField(GetText("id"), ch.Id.ToString()).AddField(GetText("created_at"), TimestampTag.FromDateTimeOffset(ch.CreatedAt))
            .AddField(GetText("users"), (await ch.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count()).AddField("NSFW", ch.IsNsfw)
            .AddField("Slowmode Interval", TimeSpan.FromSeconds(ch.SlowModeInterval).Humanize())
            .AddField("Default Thread Archive Duration", ch.DefaultArchiveDuration).WithColor(Mewdeko.OkColor);
        if (!string.IsNullOrWhiteSpace(ch.Topic))
            embed.WithDescription(ch.Topic);
        await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    [SlashCommand("userinfo", "Shows info for a mentioned or current user"), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
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
        var embed = new EmbedBuilder().AddField("Username", user.ToString()).WithColor(restUser.AccentColor ?? Mewdeko.OkColor);

        if (!string.IsNullOrWhiteSpace(user.Nickname))
            embed.AddField("Nickname", user.Nickname);

        embed.AddField("User Id", user.Id).AddField("User Type", serverUserType).AddField("Joined Server", TimestampTag.FromDateTimeOffset(user.JoinedAt.GetValueOrDefault()))
            .AddField("Joined Discord", TimestampTag.FromDateTimeOffset(user.CreatedAt)).AddField("Role Count", user.GetRoles().Count(r => r.Id != r.Guild.EveryoneRole.Id));

        if (user.Activities.Count > 0)
        {
            embed.AddField("Activities", string.Join("\n", user.Activities.Select(x => string.Format($"{x.Name}: {x.Details ?? ""}"))));
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

        var msg = await ctx.Interaction.FollowupAsync(embed: embed.Build(), components: component.Build()).ConfigureAwait(false);
        var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
        if (input == "moreinfo")
        {
            if (user.GetRoles().Any(x => x.Id != ctx.Guild.EveryoneRole.Id))
            {
                embed.AddField("Roles", string.Join("", user.GetRoles().OrderBy(x => x.Position).Select(x => x.Mention)));
            }

            embed.AddField("Deafened", user.IsDeafened);
            embed.AddField("Is VC Muted", user.IsMuted);
            embed.AddField("Is Server Muted", user.GetRoles().Contains(await muteService.GetMuteRole(ctx.Guild).ConfigureAwait(false)));
            await ctx.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Embed = embed.Build();
                x.Components = null;
            }).ConfigureAwait(false);
        }
    }

    [MessageCommand("User Info"), CheckPermissions, SlashUserPerm(ChannelPermission.SendMessages)]
    public async Task UserInfo(IMessage message) => await UserInfo(message.Author);

    [SlashCommand("userid", "Grabs the ID of a user."), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    [UserCommand("User ID")]
    public async Task UserId(IUser user = null) => await ctx.Interaction.RespondAsync(user?.Id.ToString() ?? ctx.User.Id.ToString(), ephemeral: true);

    [MessageCommand("User ID"), CheckPermissions, SlashUserPerm(GuildPermission.SendMessages)]
    public async Task UserId(IMessage message) => await UserId(message.Author);

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
                efb.WithName($"{(av.GuildAvatarId is null ? "" : "Guild")} Avatar Url").WithValue($"[Link]({avatarUrl})").WithIsInline(true))
            .WithImageUrl(avatarUrl).Build(), components: av.GuildAvatarId is null ? null : components.Build()).ConfigureAwait(false);
    }
}