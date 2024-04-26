using Discord.Commands;
using Discord.Rest;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Commands for creating and managing server invites.
    /// </summary>
    /// <param name="serv">The service for embed pagination.</param>
    /// <param name="client">The Discord client.</param>
    [Group]
    public class InviteCommands(InteractiveService serv, DiscordSocketClient client) : MewdekoSubmodule<InviteService>
    {
        /// <summary>
        /// Creates a new invite to the server with customizable options such as expiry time and maximum use count.
        /// </summary>
        /// <param name="args">Arguments to customize the invite, including expiry, max uses, and more.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         BotPerm(ChannelPermission.CreateInstantInvite), UserPerm(ChannelPermission.CreateInstantInvite),
         MewdekoOptions(typeof(InviteService.Options))]
        public async Task InviteCreate(params string[] args)
        {
            var (opts, success) = OptionsParser.ParseFrom(new InviteService.Options(), args);
            if (!success)
                return;

            var ch = (ITextChannel)ctx.Channel;
            var invite = await ch.CreateInviteAsync(opts.Expire, opts.MaxUses, opts.Temporary, opts.Unique)
                .ConfigureAwait(false);

            await ctx.Channel.SendConfirmAsync($"{ctx.User.Mention} https://discord.gg/{invite.Code}")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves detailed information about an existing invite, including member count and guild features.
        /// </summary>
        /// <param name="text">The invite code or link to gather information about.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task InviteInfo(string text)
        {
            RestGuild? guild = null;
            var invinfo = await client.Rest.GetInviteAsync(text).ConfigureAwait(false);
            if (!invinfo.GuildId.HasValue)
            {
                await ctx.Channel.SendErrorAsync(
                    "That invite was not found. Please make sure it's valid and not a vanity.", Config);
                return;
            }

            try
            {
                guild = await client.Rest.GetGuildAsync(invinfo.GuildId.Value).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            var eb = new EmbedBuilder().WithOkColor().WithTitle(invinfo.GuildName).WithThumbnailUrl(guild?.IconUrl)
                .WithDescription(
                    $"Online: {invinfo.PresenceCount}\nTotal Count: {invinfo.MemberCount}")
                .AddField("Full Link", invinfo.Url, true)
                .AddField("Channel",
                    $"[{invinfo.ChannelName}](https://discord.com/channels/{invinfo.GuildId}/{invinfo.ChannelId})",
                    true).AddField("Inviter",
                    $"{invinfo.Inviter.Mention} ({invinfo.Inviter.Id})", true);
            if (guild is not null)
            {
                eb.AddField("Partnered", guild.Features.HasFeature(GuildFeature.Partnered), true)
                    .AddField("Server Created",
                        $"{TimestampTag.FromDateTime(guild.CreatedAt.DateTime)}", true)
                    .AddField("Verified", guild.Features.HasFeature(GuildFeature.Verified), true).AddField("Discovery",
                        guild.Features.HasFeature(GuildFeature.Discoverable), true);
            }

            eb.AddField("Expires",
                invinfo.MaxAge.HasValue
                    ? TimestampTag.FromDateTime(
                        DateTime.UtcNow.Add(TimeSpan.FromDays(invinfo.MaxAge.Value)))
                    : "Permanent", true);
            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all active invites for the server, including their usage and creator information.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         BotPerm(GuildPermission.ManageGuild)]
        public async Task InviteList()
        {
            var invites = await ctx.Guild.GetInvitesAsync().ConfigureAwait(false);

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(invites.Count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var i = 1;
                var invs = invites.OrderByDescending(x => x.Uses).Skip(page * 9).Take(9);
                if (!invs.Any())
                    return new PageBuilder().WithErrorColor().WithDescription(GetText("no_invites"));
                return invs.Aggregate(new PageBuilder().WithOkColor(),
                    (acc, inv) => acc.AddField(
                        $"#{i++} {inv.Inviter.ToString().TrimTo(15)} ({inv.Uses} / {(inv.MaxUses == 0 ? "∞" : inv.MaxUses?.ToString())})",
                        inv.Url));
            }
        }

        /// <summary>
        /// Deletes an invite from the server based on its index in the invite list.
        /// </summary>
        /// <param name="index">The index of the invite to delete, starting from 1.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         BotPerm(ChannelPermission.ManageChannels), UserPerm(ChannelPermission.ManageChannels)]
        public async Task InviteDelete(int index)
        {
            if (--index < 0)
                return;
            var ch = (ITextChannel)ctx.Channel;

            var invites = await ch.GetInvitesAsync().ConfigureAwait(false);

            if (invites.Count <= index)
                return;
            var inv = invites.ElementAt(index);
            await inv.DeleteAsync().ConfigureAwait(false);

            await ReplyAsync(GetText("invite_deleted", Format.Bold(inv.Code))).ConfigureAwait(false);
        }
    }
}