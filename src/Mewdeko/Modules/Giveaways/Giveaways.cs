using System.Text.RegularExpressions;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Giveaways.Services;
using SkiaSharp;

namespace Mewdeko.Modules.Giveaways;

/// <summary>
///     Module containing commands for giveaways.
/// </summary>
/// <param name="db"></param>
/// <param name="servs"></param>
/// <param name="interactiveService"></param>
/// <param name="guildSettings"></param>
public partial class Giveaways(
    DbContextProvider dbProvider,
    IServiceProvider servs,
    InteractiveService interactiveService,
    GuildSettingsService guildSettings)
    : MewdekoModuleBase<GiveawayService>
{
    /// <summary>
    ///     Sets the dm message sent to users when they win a giveaway.
    /// </summary>
    /// <param name="message"></param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GdmMessage([Remainder] string message = null)
    {
        var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
        if (message is null)
        {
            if (await PromptUserConfirmAsync(
                    "Would you like to preview the message? Pressing no will remove the current message.",
                    Context.User.Id))
            {
                var rep = new ReplacementBuilder()
                    .WithChannel(Context.Channel)
                    .WithClient(Context.Client as DiscordShardedClient)
                    .WithServer(Context.Client as DiscordShardedClient, Context.Guild as SocketGuild)
                    .WithUser(Context.User);

                rep.WithOverride("%messagelink%",
                    () => $"https://discord.com/channels/{Context.Guild.Id}/{Context.Channel.Id}/{Context.Message.Id}");
                rep.WithOverride("%giveawayitem%", () => "test Item");
                rep.WithOverride("%giveawaywinners%", () => "10");

                var replacer = rep.Build();

                if (SmartEmbed.TryParse(replacer.Replace(gc.GiveawayEndMessage), Context.Guild.Id, out var embeds,
                        out var plaintext, out var components))
                {
                    await ctx.Channel
                        .SendMessageAsync(plaintext, embeds: embeds ?? null, components: components?.Build())
                        .ConfigureAwait(false);
                }

                else
                    await ctx.Channel.SendConfirmAsync(replacer.Replace(gc.GiveawayEndMessage)).ConfigureAwait(false);
            }
            else
            {
                gc.GiveawayEndMessage = null;
                await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
                await ctx.Channel.SendConfirmAsync("Giveaway host message removed!").ConfigureAwait(false);
            }
        }
        else
        {
            gc.GiveawayEndMessage = message;
            await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
            await ctx.Channel.SendConfirmAsync($"Giveaway host message set to {message}!").ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Sets the default giveaway banner.
    /// </summary>
    /// <param name="banner"></param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GBanner(string banner)
    {
        var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
        if (!Uri.IsWellFormedUriString(banner, UriKind.Absolute))
        {
            await ctx.Channel.SendErrorAsync("That's not a valid URL!", Config).ConfigureAwait(false);
            return;
        }

        gc.GiveawayBanner = banner;
        await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
        await ctx.Channel.SendConfirmAsync(
                "Giveaway banner set! Just keep in mind this doesn't update until the next giveaway.")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the color for the winning embed.
    /// </summary>
    /// <param name="color">The color in hex.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GWinEmbedColor(string color)
    {
        var colorVal = StringExtensions.GetHexFromColorName(color);
        if (color.StartsWith("#"))
        {
            if (SKColor.TryParse(color, out _))
                colorVal = color;
        }

        if (colorVal is not null)
        {
            var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
            gc.GiveawayEmbedColor = colorVal;
            await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
            await ctx.Channel.SendConfirmAsync(
                    "Giveaway win embed color set! Just keep in mind this doesn't update until the next giveaway.")
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel
                .SendErrorAsync(
                    "That's not a valid color! Please use proper hex (starts with #) or use html color names!", Config)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the color of the embed for giveaways.
    /// </summary>
    /// <param name="color">The color in hex.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GEmbedColor(string color)
    {
        var colorVal = StringExtensions.GetHexFromColorName(color);
        if (color.StartsWith("#"))
        {
            if (SKColor.TryParse(color, out _))
                colorVal = color;
        }

        if (colorVal is not null)
        {
            var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
            gc.GiveawayEmbedColor = colorVal;
            await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
            await ctx.Channel.SendConfirmAsync(
                    "Giveaway embed color set! Just keep in mind this doesn't update until the next giveaway.")
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel
                .SendErrorAsync(
                    "That's not a valid color! Please use proper hex (starts with #) or use html color names!", Config)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets whether to DM the winner of a giveaway.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GDm()
    {
        var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
        gc.DmOnGiveawayWin = !gc.DmOnGiveawayWin;
        await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
        await ctx.Channel.SendConfirmAsync(
                $"Giveaway DMs set to {gc.DmOnGiveawayWin}! Just keep in mind this doesn't update until the next giveaway.")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the emote used for giveaways.
    /// </summary>
    /// <param name="emote">The emote to set it to.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GEmote(IEmote emote)
    {
        try
        {
            await ctx.Message.AddReactionAsync(emote).ConfigureAwait(false);
        }
        catch
        {
            await ctx.Channel.SendErrorAsync(
                    "I'm unable to use that emote for giveaways! Most likely because I'm not in a server with it.",
                    Config)
                .ConfigureAwait(false);
            return;
        }

        await Service.SetGiveawayEmote(ctx.Guild, emote.ToString()).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync(
                $"Giveaway emote set to {emote}! Just keep in mind this doesn't update until the next giveaway.")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Rerolls a giveaway.
    /// </summary>
    /// <param name="messageid">The messageid of a giveaway</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GReroll(ulong messageid)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var gway = dbContext.Giveaways
            .GiveawaysForGuild(ctx.Guild.Id).ToList().Find(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Channel.SendErrorAsync("No Giveaway with that message ID exists! Please try again!", Config)
                .ConfigureAwait(false);
            return;
        }

        if (gway.Ended != 1)
        {
            await ctx.Channel.SendErrorAsync("This giveaway hasn't ended yet!", Config).ConfigureAwait(false);
            return;
        }

        await Service.GiveawayTimerAction(gway).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync("Giveaway Rerolled!").ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the stats for giveaways.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task GStats()
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var eb = new EmbedBuilder().WithOkColor();
        var gways = dbContext.Giveaways.GiveawaysForGuild(ctx.Guild.Id);
        if (gways.Count == 0)
        {
            await ctx.Channel.SendErrorAsync("There have been no giveaways here, so no stats!", Config)
                .ConfigureAwait(false);
        }
        else
        {
            List<ITextChannel> gchans = [];
            foreach (var i in gways)
            {
                var chan = await ctx.Guild.GetTextChannelAsync(i.ChannelId).ConfigureAwait(false);
                if (!gchans.Contains(chan))
                    gchans.Add(chan);
            }

            var amount = gways.Distinct(x => x.UserId).Count();
            eb.WithTitle("Giveaway Statistics!");
            eb.AddField("Amount of users that started giveaways", amount, true);
            eb.AddField("Total amount of giveaways", gways.Count, true);
            eb.AddField("Active Giveaways", gways.Count(x => x.Ended == 0), true);
            eb.AddField("Ended Giveaways", gways.Count(x => x.Ended == 1), true);
            eb.AddField("Giveaway Channels: Uses",
                string.Join("\n", gchans.Select(x => $"{x.Mention}: {gways.Count(s => s.ChannelId == x.Id)}")),
                true);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Quick start a giveaway.
    /// </summary>
    /// <param name="chan">The channel to start the giveaway in</param>
    /// <param name="time">The amount of time the giveaway should go on</param>
    /// <param name="winners">The amount of winners</param>
    /// <param name="what">The item to be given away</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GStart(ITextChannel chan, StoopidTime time, int winners, [Remainder] string what)
    {
        var emote = (await Service.GetGiveawayEmote(ctx.Guild.Id)).ToIEmote();
        try
        {
            await ctx.Message.AddReactionAsync(emote).ConfigureAwait(false);
        }
        catch
        {
            await ctx.Channel.SendErrorAsync(
                    "The current giveaway emote is invalid or I can't access it! Please set it again and start a new giveaway.",
                    Config)
                .ConfigureAwait(false);
            return;
        }

        var user = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = user.GetPermissions(chan);
        if (!perms.Has(ChannelPermission.AddReactions))
        {
            await ctx.Channel.SendErrorAsync("I cannot add reactions in that channel!", Config).ConfigureAwait(false);
            return;
        }

        if (!perms.Has(ChannelPermission.UseExternalEmojis) && !ctx.Guild.Emotes.Contains(emote))
        {
            await ctx.Channel.SendErrorAsync("I'm unable to use external emotes!", Config).ConfigureAwait(false);
            return;
        }

        await Service.GiveawaysInternal(chan, time.Time, what, winners, ctx.User.Id, ctx.Guild.Id,
            ctx.Channel as ITextChannel, ctx.Guild).ConfigureAwait(false);
    }

    /// <summary>
    ///     More detailed giveaway starting, lets you set a banner, ping role, and more.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GStart()
    {
        var emote = (await Service.GetGiveawayEmote(ctx.Guild.Id)).ToIEmote();
        try
        {
            await ctx.Message.AddReactionAsync(emote).ConfigureAwait(false);
        }
        catch
        {
            await ctx.Channel.SendErrorAsync(
                    "The current giveaway emote is invalid or I can't access it! Please set it again and start a new giveaway.",
                    Config)
                .ConfigureAwait(false);
            return;
        }

        var gset = await guildSettings.GetGuildConfig(ctx.Guild.Id);

        var winners = 0;
        string banner = null;
        IRole pingRole = null;
        ulong messageCount = 0;
        var host = ctx.User;
        TimeSpan time;
        var useCaptcha = false;
        var useButton = false;
        string prize = null;
        string requiredRoles = null;

        var errorEmbed = new EmbedBuilder()
            .WithErrorColor()
            .WithDescription("Either something went wrong or you input a value incorrectly! Please start over.")
            .Build();

        var promptEmbed = new EmbedBuilder()
            .WithOkColor();

        // Prompt for Giveaway Channel
        var msg = await ctx.Channel
            .SendMessageAsync(embed: promptEmbed
                .WithDescription("Please mention or provide the ID of the channel where you want to start a giveaway.")
                .Build()).ConfigureAwait(false);
        var response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
        ITextChannel channel = null;

        var channelReader = new ChannelTypeReader<ITextChannel>();
        var channelResult = await channelReader.ReadAsync(ctx, response, servs).ConfigureAwait(false);
        if (channelResult.IsSuccess)
        {
            channel = (ITextChannel)channelResult.BestMatch;
        }
        else
        {
            await msg.ModifyAsync(x => x.Embed = errorEmbed).ConfigureAwait(false);
            return;
        }

        // Check Permissions
        var botUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var permissions = botUser.GetPermissions(channel);
        if (!permissions.Has(ChannelPermission.AddReactions))
        {
            await ctx.Channel.SendErrorAsync("I cannot add reactions in that channel!", Config).ConfigureAwait(false);
            return;
        }

        if (!permissions.Has(ChannelPermission.UseExternalEmojis) && !ctx.Guild.Emotes.Contains(emote))
        {
            await ctx.Channel.SendErrorAsync("I'm unable to use external emotes!", Config).ConfigureAwait(false);
            return;
        }

        // Number of Winners
        await msg.ModifyAsync(x => x.Embed = promptEmbed.WithDescription("How many winners will there be?").Build())
            .ConfigureAwait(false);
        response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
        if (!int.TryParse(response, out winners) || winners <= 0)
        {
            await msg.ModifyAsync(x => x.Embed = errorEmbed).ConfigureAwait(false);
            return;
        }

        // Prize
        await msg.ModifyAsync(x => x.Embed = promptEmbed.WithDescription("What is the prize/item?").Build())
            .ConfigureAwait(false);
        prize = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);

        // Duration
        await msg.ModifyAsync(x =>
            x.Embed = promptEmbed.WithDescription("How long will this giveaway last? Use the format `1mo,2d,3h,4m,5s`.")
                .Build()).ConfigureAwait(false);
        response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
        try
        {
            var duration = StoopidTime.FromInput(response);
            time = duration.Time;
        }
        catch
        {
            await msg.ModifyAsync(x => x.Embed = errorEmbed).ConfigureAwait(false);
            return;
        }

        // Host
        await msg.ModifyAsync(x =>
            x.Embed = promptEmbed
                .WithDescription(
                    "Who is the giveaway host? You can mention them or provide an ID, or say `none`/`skip` to set yourself as the host.")
                .Build()).ConfigureAwait(false);
        response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
        if (!response.Equals("none", StringComparison.OrdinalIgnoreCase) &&
            !response.Equals("skip", StringComparison.OrdinalIgnoreCase))
        {
            var userReader = new UserTypeReader<IUser>();
            var userResult = await userReader.ReadAsync(ctx, response, servs).ConfigureAwait(false);
            if (userResult.IsSuccess)
            {
                host = (IUser)userResult.BestMatch;
            }
            else
            {
                await msg.ModifyAsync(x => x.Embed = errorEmbed).ConfigureAwait(false);
                return;
            }
        }

        // Message Requirement
        await msg.ModifyAsync(x =>
                x.Embed = promptEmbed.WithDescription("Would you like to set a message requirement? (yes/no)").Build())
            .ConfigureAwait(false);
        response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
        if (response.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            response.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            await msg.ModifyAsync(x =>
                    x.Embed = promptEmbed.WithDescription("How many messages is the user required to send?").Build())
                .ConfigureAwait(false);
            response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
            if (!ulong.TryParse(response, out messageCount))
            {
                await msg.ModifyAsync(x => x.Embed = errorEmbed).ConfigureAwait(false);
                return;
            }
        }

        // Banner
        await msg.ModifyAsync(x =>
                x.Embed = promptEmbed.WithDescription("Would you like to set a banner? (yes/no)").Build())
            .ConfigureAwait(false);
        response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
        if (response.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            response.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            await msg.ModifyAsync(x =>
                x.Embed = promptEmbed.WithDescription("Please provide a link to the banner or attach an image.")
                    .Build()).ConfigureAwait(false);
            var bannerResponse = await NextFullMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
            if (bannerResponse.Attachments.Any())
            {
                banner = bannerResponse.Attachments.First().Url;
            }
            else if (Uri.IsWellFormedUriString(bannerResponse.Content, UriKind.Absolute))
            {
                banner = bannerResponse.Content;
            }
            else
            {
                await msg.ModifyAsync(x => x.Embed = errorEmbed).ConfigureAwait(false);
                return;
            }
        }

        // Ping Role
        if (gset.GiveawayPingRole != 0)
        {
            await msg.ModifyAsync(x =>
                    x.Embed = promptEmbed
                        .WithDescription("Would you like to override the default ping role in configs? (yes/no)")
                        .Build())
                .ConfigureAwait(false);
            response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
            if (response.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                response.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                await msg.ModifyAsync(x =>
                        x.Embed = promptEmbed.WithDescription("Please provide a role mention for the ping role.")
                            .Build())
                    .ConfigureAwait(false);
                response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                var role = ParseRole(response);
                if (role != null)
                {
                    pingRole = role;
                }
                else
                {
                    await msg.ModifyAsync(x => x.Embed = errorEmbed).ConfigureAwait(false);
                    return;
                }
            }
        }
        else
        {
            await msg.ModifyAsync(x =>
                    x.Embed = promptEmbed.WithDescription("Would you like to set a ping role? (yes/no)").Build())
                .ConfigureAwait(false);
            response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
            if (response.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                response.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                await msg.ModifyAsync(x =>
                        x.Embed = promptEmbed.WithDescription("Please provide a role mention for the ping role.")
                            .Build())
                    .ConfigureAwait(false);
                response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                var role = ParseRole(response);
                if (role != null)
                {
                    pingRole = role;
                }
                else
                {
                    await msg.ModifyAsync(x => x.Embed = errorEmbed).ConfigureAwait(false);
                    return;
                }
            }
        }

        // Use Button or Captcha
        await msg.ModifyAsync(x =>
            x.Embed = promptEmbed
                .WithDescription("Would you like to use a button or captcha for entry instead of reactions? (yes/no)")
                .Build()).ConfigureAwait(false);
        response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
        if (response.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            response.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            await msg.ModifyAsync(x =>
                    x.Embed = promptEmbed
                        .WithDescription("Type `button` for a regular button or `captcha` for a captcha entry.")
                        .Build())
                .ConfigureAwait(false);
            response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
            if (response.Equals("button", StringComparison.OrdinalIgnoreCase))
            {
                useButton = true;
            }
            else if (response.Equals("captcha", StringComparison.OrdinalIgnoreCase))
            {
                useCaptcha = true;
            }
            else
            {
                await msg.ModifyAsync(x => x.Embed = errorEmbed).ConfigureAwait(false);
                return;
            }
        }

        // Role Requirements
        await msg.ModifyAsync(x =>
                x.Embed = promptEmbed.WithDescription("Would you like to set up role requirements? (yes/no)").Build())
            .ConfigureAwait(false);
        response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
        if (response.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            response.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            await msg.ModifyAsync(x =>
                x.Embed = promptEmbed.WithDescription("Please mention the role(s) that are required for this giveaway.")
                    .Build()).ConfigureAwait(false);
            response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
            var roles = ParseRoles(response);
            if (roles.Count > 0)
            {
                requiredRoles = string.Join(" ", roles.Select(r => r.Id));
            }
            else
            {
                await msg.ModifyAsync(x =>
                    x.Embed = promptEmbed.WithDescription("No valid roles mentioned. Skipping role requirements.")
                        .Build()).ConfigureAwait(false);
            }
        }

        await msg.DeleteAsync().ConfigureAwait(false);

        // Start the Giveaway
        await Service.GiveawaysInternal(channel, time, prize, winners, host.Id, ctx.Guild.Id,
            ctx.Channel as ITextChannel, ctx.Guild, requiredRoles, null, null, null, banner,
            pingRole, useButton, useCaptcha, messageCount).ConfigureAwait(false);
    }

// Helper Methods
    private IRole? ParseRole(string input)
    {
        var roleIdMatches = MyRegex1().Matches(input).Select(m => ulong.Parse(m.Value));
        return roleIdMatches.Select(id => ctx.Guild.GetRole(id)).FirstOrDefault(r => r != null);
    }

    private List<IRole> ParseRoles(string input)
    {
        var roleIdMatches = MyRegex1().Matches(input).Select(m => ulong.Parse(m.Value));
        return roleIdMatches.Select(id => ctx.Guild.GetRole(id)).Where(r => r != null).ToList();
    }

    /// <summary>
    ///     Lists all active giveaways.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GList()
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var gways = dbContext.Giveaways.GiveawaysForGuild(ctx.Guild.Id).Where(x => x.Ended == 0);
        if (!gways.Any())
        {
            await ctx.Channel.SendErrorAsync("No active giveaways", Config).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(gways.Count() / 5)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            return new PageBuilder().WithOkColor().WithTitle($"{gways.Count()} Active Giveaways")
                .WithDescription(string.Join("\n\n",
                    await gways.Skip(page * 5).Take(5).Select(async x =>
                            $"{x.MessageId}\nPrize: {x.Item}\nWinners: {x.Winners}\nLink: {await GetJumpUrl(x.ChannelId, x.MessageId).ConfigureAwait(false)}")
                        .GetResults()
                        .ConfigureAwait(false)));
        }
    }

    private async Task<string> GetJumpUrl(ulong channelId, ulong messageId)
    {
        var channel = await ctx.Guild.GetTextChannelAsync(channelId).ConfigureAwait(false);
        var message = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
        return message.GetJumpUrl();
    }

    /// <summary>
    ///     Ends a giveaway.
    /// </summary>
    /// <param name="messageid">The messageid of the giveaway to end</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task GEnd(ulong messageid)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var gway = dbContext.Giveaways
            .GiveawaysForGuild(ctx.Guild.Id).ToList().Find(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Channel.SendErrorAsync("No Giveaway with that message ID exists! Please try again!", Config)
                .ConfigureAwait(false);
        }

        if (gway.Ended == 1)
        {
            await ctx.Channel.SendErrorAsync(
                    $"This giveaway has already ended! Plase use `{await guildSettings.GetPrefix(ctx.Guild)}greroll {messageid}` to reroll!",
                    Config)
                .ConfigureAwait(false);
        }
        else
        {
            await Service.GiveawayTimerAction(gway).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Giveaway ended!").ConfigureAwait(false);
        }
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex MyRegex1();

    [GeneratedRegex(@"\d+")]
    private static partial Regex MyRegex2();
}