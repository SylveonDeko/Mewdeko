using System.Threading.Tasks;
using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    [Group]
    public class InfoCommands : MewdekoSubmodule<UtilityService>
    {
        private readonly DiscordSocketClient client;
        private readonly MuteService muteService;

        public InfoCommands(DiscordSocketClient client, MuteService muteService)
        {
            this.client = client;
            this.muteService = muteService;
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task RInfo(IRole role)
        {
            var eb = new EmbedBuilder().WithTitle(role.Name)
                .AddField("Users in role",
                    (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => x.RoleIds.Contains(role.Id)))
                .AddField("Is Mentionable", role.IsMentionable)
                .AddField("Is Hoisted", role.IsHoisted).AddField("Color", role.Color.RawValue)
                .AddField("Is Managed", role.IsManaged)
                .AddField("Permissions", string.Join(",", role.Permissions))
                .AddField("Creation Date", TimestampTag.FromDateTimeOffset(role.CreatedAt))
                .AddField("Position", role.Position)
                .AddField("ID", role.Id)
                .WithThumbnailUrl(role.GetIconUrl())
                .WithColor(role.Color);
            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task VInfo([Remainder] IVoiceChannel? channel = null)
        {
            var voiceChannel = ((IGuildUser)ctx.User).VoiceChannel;
            var eb = new EmbedBuilder();
            switch (voiceChannel)
            {
                case null when channel == null:
                    await ctx.Channel.SendErrorAsync(
                        "You arent in a voice channel, and you haven't mentioned either to use this command!").ConfigureAwait(false);
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
                    await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Fetch(ulong id)
        {
            var usr = await client.Rest.GetUserAsync(id).ConfigureAwait(false);
            if (usr is null)
            {
                await ctx.Channel.SendErrorAsync("That user could not be found. Please ensure that was the correct ID.");
            }
            else
            {
                var embed = new EmbedBuilder()
                    .WithTitle("info for fetched user")
                    .AddField("Username", usr)
                    .AddField("Created At", TimestampTag.FromDateTimeOffset(usr.CreatedAt))
                    .AddField("Public Flags", usr.PublicFlags)
                    .WithImageUrl(usr.RealAvatarUrl().ToString())
                    .WithOkColor();
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task ServerInfo(string? guildName = null)
        {
            var channel = (ITextChannel)ctx.Channel;
            guildName = guildName?.ToUpperInvariant();
            SocketGuild guild;
            if (string.IsNullOrWhiteSpace(guildName))
            {
                guild = (SocketGuild)channel.Guild;
            }
            else
            {
                guild = client.Guilds.FirstOrDefault(
                    g => string.Equals(g.Name, guildName, StringComparison.InvariantCultureIgnoreCase));
            }

            if (guild == null)
                return;
            var ownername = guild.GetUser(guild.OwnerId);
            var textchn = guild.TextChannels.Count;
            var voicechn = guild.VoiceChannels.Count;

            var component = new ComponentBuilder().WithButton("More Info", "moreinfo");
            var embed = new EmbedBuilder()
                .WithAuthor(eab => eab.WithName(GetText("server_info")))
                .WithTitle(guild.Name)
                .AddField("Id", guild.Id.ToString())
                .AddField("Owner", ownername.Mention)
                .AddField("Total Users", guild.Users.Count.ToString())
                .AddField("Created On", TimestampTag.FromDateTimeOffset(guild.CreatedAt))
                .WithColor(Mewdeko.OkColor);
            if (guild.SplashUrl != null)
                embed.WithImageUrl($"{guild.SplashUrl}?size=2048");
            if (Uri.IsWellFormedUriString(guild.IconUrl, UriKind.Absolute))
                embed.WithThumbnailUrl(guild.IconUrl);
            if (guild.Emotes.Count > 0)
            {
                embed.AddField(fb =>
                    fb.WithName($"{GetText("custom_emojis")}({guild.Emotes.Count})")
                        .WithValue(string.Join(" ", guild.Emotes
                                .Shuffle()
                                .Take(30)
                                .Select(e => $"{e}"))
                            .TrimTo(1024)));
            }

            var msg = await ctx.Channel.SendMessageAsync(embed: embed.Build(), components: component.Build()).ConfigureAwait(false);
            var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
            if (input == "moreinfo")
            {
                var vals = Enum.GetValues(typeof(GuildFeature)).Cast<GuildFeature>();
                var setFeatures = vals.Where(x => guild.Features.Value.HasFlag(x));
                embed
                    .AddField("Bots", (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => x.IsBot))
                    .AddField("Users", (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => !x.IsBot))
                    .AddField("Text Channels", textchn.ToString())
                    .AddField("Voice Channels", voicechn.ToString())
                    .AddField("Roles", (guild.Roles.Count - 1).ToString())
                    .AddField("Server Features", Format.Code(string.Join("\n", setFeatures)));
                await msg.ModifyAsync(x =>
                {
                    x.Embed = embed.Build();
                    x.Components = null;
                }).ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task ChannelInfo(ITextChannel? channel = null)
        {
            var ch = channel ?? (ITextChannel)ctx.Channel;
            var embed = new EmbedBuilder()
                .WithTitle(ch.Name)
                .AddField(GetText("id"), ch.Id.ToString())
                .AddField(GetText("created_at"), TimestampTag.FromDateTimeOffset(ch.CreatedAt))
                .AddField(GetText("users"), (await ch.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count())
                .AddField("NSFW", ch.IsNsfw)
                .AddField("Slowmode Interval", TimeSpan.FromSeconds(ch.SlowModeInterval).Humanize())
                .AddField("Default Thread Archive Duration", ch.DefaultArchiveDuration)
                .WithColor(Mewdeko.OkColor);
            if (!string.IsNullOrWhiteSpace(ch.Topic))
                embed.WithDescription(ch.Topic);
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task UserInfo(IGuildUser? usr = null)
        {
            var component = new ComponentBuilder().WithButton("More Info", "moreinfo");
            var user = usr ?? ctx.User as IGuildUser;
            var userbanner = (await client.Rest.GetUserAsync(user.Id).ConfigureAwait(false)).GetBannerUrl(size: 2048);
            var serverUserType = user.GuildPermissions.Administrator ? "Administrator" : "Regular User";
            var restUser = await client.Rest.GetUserAsync(user.Id);
            var embed = new EmbedBuilder()
                .AddField("Username", user.ToString())
                .WithColor(restUser.AccentColor ?? Mewdeko.OkColor);

            if (!string.IsNullOrWhiteSpace(user.Nickname))
                embed.AddField("Nickname", user.Nickname);

            embed.AddField("User Id", user.Id)
                .AddField("User Type", serverUserType)
                .AddField("Joined Server", TimestampTag.FromDateTimeOffset(user.JoinedAt.GetValueOrDefault()))
                .AddField("Joined Discord", TimestampTag.FromDateTimeOffset(user.CreatedAt))
                .AddField("Role Count", user.GetRoles().Count(r => r.Id != r.Guild.EveryoneRole.Id));

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

            var msg = await ctx.Channel.SendMessageAsync(embed: embed.Build(), components: component.Build()).ConfigureAwait(false);
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
                embed.AddField("Is Server Muted", user.GetRoles().Contains(await muteService.GetMuteRole(ctx.Guild).ConfigureAwait(false)));
                await msg.ModifyAsync(x =>
                {
                    x.Embed = embed.Build();
                    x.Components = null;
                }).ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Avatar([Remainder] IGuildUser? usr = null)
        {
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
                avatarUrl = usr.GuildAvatarId.StartsWith("a_", StringComparison.InvariantCulture)
                    ? $"{DiscordConfig.CDNUrl}guilds/{ctx.Guild.Id}/users/{usr.Id}/avatars/{av.GuildAvatarId}.gif?size=2048"
                    : $"{DiscordConfig.CDNUrl}guilds/{ctx.Guild.Id}/users/{usr.Id}/avatars/{av.GuildAvatarId}.png?size=2048";

            await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .AddField(efb => efb.WithName("Username").WithValue(usr.ToString()).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName($"{(av.GuildAvatarId is null ? "" : "Guild")} Avatar Url").WithValue($"[Link]({avatarUrl})").WithIsInline(true))
                .WithImageUrl(avatarUrl).Build(), components: av.GuildAvatarId is null ? null : components.Build()).ConfigureAwait(false);
        }
    }
}