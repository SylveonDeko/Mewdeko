using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Bridge.Services;

namespace Mewdeko.Modules.Bridge;

/// <summary>
///     How a pair handles bridges the other server proposes.
/// </summary>
public enum BridgeApprovalChoice
{
    /// <summary>Each bridge waits for approval.</summary>
    Approve,

    /// <summary>Bridges are accepted automatically.</summary>
    Auto
}

public partial class BridgeSlash
{
    /// <summary>
    ///     Slash commands for pairing this server with a server on Fluxer.
    /// </summary>
    [Group("pair", "Pair this server with a server on Fluxer")]
    public class BridgePairCommands : MewdekoSlashSubmodule<BridgeService>
    {
        /// <summary>
        ///     Makes a pair code.
        /// </summary>
        [SlashCommand("code", "Make a code to pair this server from Fluxer")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Code() => RunAsync("pair", null);

        /// <summary>
        ///     Pairs this server with the server a code was made for.
        /// </summary>
        /// <param name="code">The pair code from Fluxer.</param>
        [SlashCommand("link", "Pair this server using a code from Fluxer")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Link(string code) => RunAsync("pair", code);

        /// <summary>
        ///     Lists the servers this server is paired with.
        /// </summary>
        [SlashCommand("list", "List the servers this server is paired with")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task List() => RunAsync("pairs", null);

        /// <summary>
        ///     Lists the channels a paired server shares.
        /// </summary>
        /// <param name="pair">The pair number or name, when there is more than one.</param>
        [SlashCommand("channels", "List the channels a paired server shares with this one")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Channels(string? pair = null) => RunAsync("channels", pair);

        /// <summary>
        ///     Proposes bridging a channel here with a channel a paired server shares.
        /// </summary>
        /// <param name="channel">The channel in this server.</param>
        /// <param name="target">The shared channel's name or id.</param>
        /// <param name="pair">The pair number or name, when there is more than one.</param>
        [SlashCommand("connect", "Bridge a channel here with a channel a paired server shares")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Connect(ITextChannel channel, string target, string? pair = null) =>
            RunAsync("connect", $"{channel.Id} {target} {pair}");

        /// <summary>
        ///     Lists the pending bridge proposals.
        /// </summary>
        [SlashCommand("proposals", "List the pending bridge proposals")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Proposals() => RunAsync("proposals", null);

        /// <summary>
        ///     Accepts a bridge proposal.
        /// </summary>
        /// <param name="proposal">The proposal number or id.</param>
        [SlashCommand("accept", "Accept a bridge proposal")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Accept(string proposal) => RunAsync("accept", proposal);

        /// <summary>
        ///     Declines a bridge proposal, or cancels one made here.
        /// </summary>
        /// <param name="proposal">The proposal number or id.</param>
        [SlashCommand("decline", "Decline a bridge proposal, or cancel one you made")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Decline(string proposal) => RunAsync("decline", proposal);

        /// <summary>
        ///     Shares a channel with a pair.
        /// </summary>
        /// <param name="channel">The channel to share.</param>
        /// <param name="pair">The pair number or name, when there is more than one.</param>
        [SlashCommand("share", "Share a channel with a paired server")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Share(ITextChannel channel, string? pair = null) => RunAsync("share", $"{channel.Id} {pair}");

        /// <summary>
        ///     Hides a channel from a pair.
        /// </summary>
        /// <param name="channel">The channel to hide.</param>
        /// <param name="pair">The pair number or name, when there is more than one.</param>
        [SlashCommand("hide", "Hide a channel from a paired server")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Hide(ITextChannel channel, string? pair = null) => RunAsync("hide", $"{channel.Id} {pair}");

        /// <summary>
        ///     Chooses whether bridges proposed by a pair wait for approval.
        /// </summary>
        /// <param name="mode">Approve each bridge or accept them automatically.</param>
        /// <param name="pair">The pair number or name, when there is more than one.</param>
        [SlashCommand("approval", "Choose whether bridges a paired server proposes wait for approval")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Approval(BridgeApprovalChoice mode, string? pair = null) =>
            RunAsync("approval", $"{mode} {pair}");

        /// <summary>
        ///     Ends a pair.
        /// </summary>
        /// <param name="pair">The pair number or name, when there is more than one.</param>
        /// <param name="keep">Whether the bridges made through the pair stay.</param>
        [SlashCommand("unpair", "End a pair, removing the bridges made through it unless you keep them")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public Task Unpair(string? pair = null, bool keep = false) =>
            RunAsync("unpair", keep ? $"{pair} keep" : pair);

        private async Task RunAsync(string action, string? argument)
        {
            await DeferAsync(true);
            var reply = await Service.Pairs.RunAsync(action, argument, ctx.Guild.Id, ctx.User.Id, ctx.Channel.Id, "/");
            if (!reply.Success)
                await EphemeralReplyErrorAsync(reply.Text);
            else if (reply.Title is null)
                await EphemeralReplyConfirmAsync(reply.Text);
            else
                await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor().WithTitle(reply.Title)
                    .WithDescription(reply.Text).Build(), ephemeral: true);
        }
    }
}
