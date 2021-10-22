using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Services;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class InfoCommands : MewdekoSubmodule<UtilityService>
        {
            private readonly DiscordSocketClient _client;
            private readonly IStatsService _stats;

            public InfoCommands(DiscordSocketClient client, IStatsService stats)
            {
                _client = client;
                _stats = stats;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task VInfo([Remainder]IVoiceChannel channel = null)
            {
                var voiceChannel = ((IGuildUser)ctx.User).VoiceChannel;
                var eb = new EmbedBuilder();
                if (voiceChannel == null && channel == null)
                {
                    await ctx.Channel.SendErrorAsync(
                        "You arent in a voice channel, and you haven't mentioned either to use this command!");
                    return;
                }

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (voiceChannel is null && channel is not null)
                {
                    
                    eb.WithTitle(channel.Name);
                    eb.AddField("Users", channel.GetUsersAsync().FlattenAsync().Result.Count());
                    eb.AddField("Created On", channel.CreatedAt);
                    eb.AddField("Bitrate", channel.Bitrate);
                    eb.AddField("User Limit", channel.UserLimit == null ? "Infinite" : channel.UserLimit);
                    eb.AddField("Channel ID", channel.Id);
                    eb.WithOkColor();
                    await ctx.Channel.SendMessageAsync(embed: eb.Build());
                }
                if (voiceChannel is not null && channel is not null)
                {
                    
                    eb.WithTitle(channel.Name);
                    eb.AddField("Users", channel.GetUsersAsync().FlattenAsync().Result.Count());
                    eb.AddField("Created On", channel.CreatedAt);
                    eb.AddField("Bitrate", channel.Bitrate);
                    eb.AddField("User Limit", channel.UserLimit == null ? "Infinite" : channel.UserLimit);
                    eb.AddField("Channel ID", channel.Id);
                    eb.WithOkColor();
                    await ctx.Channel.SendMessageAsync(embed: eb.Build());
                }
                if (voiceChannel is not null && channel is null)
                {
                    eb.WithTitle(voiceChannel.Name);
                    eb.AddField("Users", voiceChannel.GetUsersAsync().FlattenAsync().Result.Count());
                    eb.AddField("Created On", voiceChannel.CreatedAt);
                    eb.AddField("Bitrate", voiceChannel.Bitrate);
                    eb.AddField("User Limit", voiceChannel.UserLimit == null ? "Infinite" : voiceChannel.UserLimit);
                    eb.AddField("Channel ID", voiceChannel.Id);
                    eb.WithOkColor();
                    await ctx.Channel.SendMessageAsync(embed: eb.Build());
                }
            }
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Fetch(ulong id)
            {
                var usr = await _client.Rest.GetUserAsync(id);
                if (usr is null)
                {
                    IUserMessage message = null;
                    foreach (var i in ctx.Guild.GetTextChannelsAsync().Result)
                    {
                        var e = await i.GetMessageAsync(id);
                        if (e is not null)
                            message = e as IUserMessage;
                        continue;
                    }

                    var eb = new EmbedBuilder()
                        .WithTitle("Message Info");
                    if (message.Embeds.Any())
                    {
                        eb.AddField("Embeds", message.Embeds.Count);
                    }

                    if (!string.IsNullOrEmpty(message.Content))
                    {
                        eb.AddField("Message Content (Limited to 60 characters)", message.Content.Truncate(60));
                    }

                    eb.WithAuthor(message.Author);
                    eb.AddField("Time Sent", message.Timestamp);
                    await ctx.Channel.SendMessageAsync(embed: eb.Build());
                }
                else
                {


                    var embed = new EmbedBuilder()
                        .WithTitle("info for fetched user")
                        .WithDescription("User: " + usr.Username + "#" + usr.Discriminator + "\nUser Created At: " +
                                         usr.CreatedAt)
                        .WithImageUrl(usr.RealAvatarUrl().ToString())
                        .WithOkColor();
                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ServerInfo(string guildName = null)
            {
                var channel = (ITextChannel)ctx.Channel;
                guildName = guildName?.ToUpperInvariant();
                SocketGuild guild;
                if (string.IsNullOrWhiteSpace(guildName))
                    guild = (SocketGuild)channel.Guild;
                else
                    guild = _client.Guilds.FirstOrDefault(
                        g => g.Name.ToUpperInvariant() == guildName.ToUpperInvariant());
                if (guild == null)
                    return;
                var ownername = guild.GetUser(guild.OwnerId);
                var textchn = guild.TextChannels.Count();
                var voicechn = guild.VoiceChannels.Count();

                var createdAt = new DateTime(2015, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(guild.Id >> 22);
                var list = new List<string>();
                foreach (var i in guild.Features)
                {
                    var e = i.Replace("_", " ");
                    list.Add(e.ToTitleCase());
                }

                var embed = new EmbedBuilder()
                    .WithAuthor(eab => eab.WithName(GetText("server_info")))
                    .WithTitle(guild.Name)
                    .AddField(fb => fb.WithName(GetText("id")).WithValue(guild.Id.ToString()))
                    .AddField(fb => fb.WithName(GetText("owner")).WithValue(ownername.Mention))
                    .AddField(fb =>
                        fb.WithName(GetText("members")).WithValue(guild.MemberCount.ToString()))
                    .AddField(fb =>
                        fb.WithName(GetText("text_channels")).WithValue(textchn.ToString()))
                    .AddField(fb =>
                        fb.WithName(GetText("voice_channels")).WithValue(voicechn.ToString()))
                    .AddField(fb =>
                        fb.WithName(GetText("created_at")).WithValue($"{createdAt:MM/dd/yyyy HH:mm}"))
                    .AddField(fb =>
                        fb.WithName(GetText("roles")).WithValue((guild.Roles.Count - 1).ToString()))
                    .AddField(fb =>
                        fb.WithName(GetText("features")).WithValue($"```\n{string.Join("\n", list)}```")
                            .WithIsInline(true))
                    .WithImageUrl($"https://cdn.discordapp.com/splashes/{guild.Id}/{guild.SplashId}.png?size=4096")
                    .WithColor(Mewdeko.Services.Mewdeko.OkColor);
                if (Uri.IsWellFormedUriString(guild.IconUrl, UriKind.Absolute))
                    embed.WithThumbnailUrl(guild.IconUrl);
                if (guild.Emotes.Any())
                    embed.AddField(fb =>
                        fb.WithName(GetText("custom_emojis") + $"({guild.Emotes.Count})")
                            .WithValue(string.Join(" ", guild.Emotes
                                    .Shuffle()
                                    .Take(30)
                                    .Select(e => $"{e}"))
                                .TrimTo(1024)));
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChannelInfo(ITextChannel channel = null)
            {
                var ch = channel ?? (ITextChannel)ctx.Channel;
                if (ch == null)
                    return;
                var createdAt = new DateTime(2015, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ch.Id >> 22);
                var usercount = (await ch.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count();
                var embed = new EmbedBuilder()
                    .WithTitle(ch.Name)
                    .WithDescription(ch.Topic?.SanitizeMentions(true))
                    .AddField(fb => fb.WithName(GetText("id")).WithValue(ch.Id.ToString()).WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName(GetText("created_at")).WithValue($"{createdAt:dd.MM.yyyy HH:mm}")
                            .WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("users")).WithValue(usercount.ToString()).WithIsInline(true))
                    .WithColor(Mewdeko.Services.Mewdeko.OkColor);
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task UserInfo(IGuildUser usr = null)
            {
                var user = usr ?? ctx.User as IGuildUser;

                if (user == null)
                    return;

                var embed = new EmbedBuilder()
                    .AddField(fb =>
                        fb.WithName(GetText("name")).WithValue($"**{user.Username}**#{user.Discriminator}")
                            .WithIsInline(true));
                if (!string.IsNullOrWhiteSpace(user.Nickname))
                    embed.AddField(fb => fb.WithName(GetText("nickname")).WithValue(user.Nickname).WithIsInline(true));
                embed.AddField(fb => fb.WithName(GetText("id")).WithValue(user.Id.ToString()).WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName(GetText("joined_server"))
                            .WithValue($"{user.JoinedAt?.ToString("MM/dd/yyyy HH:mm") ?? "?"}").WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName(GetText("joined_discord")).WithValue($"{user.CreatedAt:MM/dd/yyyy HH:mm}")
                            .WithIsInline(true))
                    .WithColor(Mewdeko.Services.Mewdeko.OkColor);
                if (!user.GetRoles().Any())
                    embed.AddField(fb => fb.WithName(GetText("roles")).WithValue("None"));
                else
                    embed.AddField(fb =>
                        fb.WithName(GetText("roles"))
                            .WithValue(
                                $"{string.Join(" ", user.GetRoles().Where(r => r.Id != r.Guild.EveryoneRole.Id).OrderByDescending(r => r.Position).Select(r => r.Mention).Take(30))}")
                            .WithIsInline(false));
                if (user.Activities?.FirstOrDefault()?.Name != null)
                    embed.AddField(fb =>
                        fb.WithName("User Activity").WithValue(user.Activities?.FirstOrDefault()?.Type + ": " +
                                                               user.Activities.FirstOrDefault().Name));
                else
                    embed.AddField(fb => fb.WithName("User Activity").WithValue("None"));
                var av = user.RealAvatarUrl();
                if (av != null && av.IsAbsoluteUri)
                    embed.WithImageUrl(av.ToString());
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }


            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [OwnerOnly]
            public async Task Activity(int page = 1)
            {
                const int activityPerPage = 10;
                page -= 1;

                if (page < 0)
                    return;

                var startCount = page * activityPerPage;

                var str = new StringBuilder();
                foreach (var kvp in CmdHandler.UserMessagesSent.OrderByDescending(kvp => kvp.Value)
                    .Skip(page * activityPerPage).Take(activityPerPage))
                    str.AppendLine(GetText("activity_line",
                        ++startCount,
                        Format.Bold(kvp.Key.ToString()),
                        kvp.Value / _stats.GetUptime().TotalSeconds, kvp.Value));

                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithTitle(GetText("activity_page", page + 1))
                    .WithOkColor()
                    .WithFooter(efb => efb.WithText(GetText("activity_users_total",
                        CmdHandler.UserMessagesSent.Count)))
                    .WithDescription(str.ToString())).ConfigureAwait(false);
            }
        }
    }
}