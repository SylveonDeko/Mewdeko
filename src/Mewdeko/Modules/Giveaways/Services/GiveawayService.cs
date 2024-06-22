using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Serilog;
using Swan;

namespace Mewdeko.Modules.Giveaways.Services;

/// <summary>
/// Service for handling giveaways.
/// </summary>
/// <param name="client">The discord client.</param>
/// <param name="db">The database.</param>
/// <param name="creds">The bot credentials.</param>
/// <param name="guildSettings">Guild Settings Service</param>
public class GiveawayService(
    DiscordShardedClient client,
    MewdekoContext dbContext,
    IBotCredentials creds,
    GuildSettingsService guildSettings,
    BotConfig config)
    : INService, IReadyExecutor
{
    /// <summary>
    /// Asynchronous method to handle the execution of the giveaway loop when the bot is ready.
    /// This method continuously checks for active giveaways and processes them.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        Log.Information($"Starting {this.GetType()} Ready Loop");
        Log.Information("Giveaway Loop Started");
        while (true)
        {
            await Task.Delay(2000).ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var giveawaysEnumerable = GetGiveawaysBeforeAsync(now);
                if (!giveawaysEnumerable.Any())
                    continue;

                Log.Information("Executing {Count} giveaways", giveawaysEnumerable.Count());

                // make groups of 5, with 1.5 second inbetween each one to ensure against ratelimits
                var i = 0;
                foreach (var group in giveawaysEnumerable
                             .GroupBy(_ => ++i / ((giveawaysEnumerable.Count() / 5) + 1)))
                {
                    var executedGiveaways = group.ToList();
                    await Task.WhenAll(executedGiveaways.Select(x => GiveawayTimerAction(x))).ConfigureAwait(false);
                    await UpdateGiveaways(executedGiveaways).ConfigureAwait(false);
                    await Task.Delay(1500).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Error in Giveaway loop: {ExMessage}", ex.Message);
                Log.Warning(ex.ToString());
            }
        }
    }


    /// <summary>
    /// Asynchronously sets the emote to be used for giveaways in the specified guild.
    /// </summary>
    /// <param name="guild">The guild where the emote is to be set.</param>
    /// <param name="emote">The emote to set.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetGiveawayEmote(IGuild guild, string emote)
    {

        var gc = await dbContext.ForGuildId(guild.Id, set => set);
        gc.GiveawayEmote = emote;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Asynchronously retrieves the emote used for giveaways in the guild with the specified ID.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The emote used for giveaways.</returns>
    public async Task<string> GetGiveawayEmote(ulong id)
        => (await guildSettings.GetGuildConfig(id)).GiveawayEmote;

    /// <summary>
    /// Asynchronously updates the database with the provided list of giveaways.
    /// </summary>
    /// <param name="g">The list of giveaways to update.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task UpdateGiveaways(List<Database.Models.Giveaways> g)
    {

        foreach (var i in g)
        {
            var toupdate = await dbContext.Giveaways.FindAsync(i.Id);
            if (toupdate == null) continue;
            toupdate.When = i.When;
            toupdate.BlacklistRoles = i.BlacklistRoles;
            toupdate.BlacklistUsers = i.BlacklistUsers;
            toupdate.ChannelId = i.ChannelId;
            toupdate.Ended = 1;
            toupdate.MessageId = i.MessageId;
            toupdate.RestrictTo = i.RestrictTo;
            toupdate.Item = i.Item;
            toupdate.UserId = i.UserId;
            toupdate.Winners = i.Winners;
            toupdate.Emote = i.Emote;
            dbContext.Giveaways.Update(toupdate);
        }
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }


    /// <summary>
    /// Retrieves the giveaways that have ended before the specified time asynchronously.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="Database.Models.Giveaways"/>.</returns>
    private IEnumerable<Database.Models.Giveaways> GetGiveawaysBeforeAsync(DateTime now)
    {


        IEnumerable<Database.Models.Giveaways> giveaways =
            // Linq to db queries because npgsql is special, again.
            dbContext.Giveaways
                .ToLinqToDB()
                .Where(x => x.Ended != 1 && x.When < now).ToList();

        return giveaways;
    }

    /// <summary>
    /// Initiates a giveaway with specified parameters.
    /// </summary>
    /// <param name="chan">The text channel where the giveaway will be initiated.</param>
    /// <param name="ts">The duration of the giveaway.</param>
    /// <param name="item">The item or prize being given away.</param>
    /// <param name="winners">The number of winners for the giveaway.</param>
    /// <param name="host">The ID of the user hosting the giveaway.</param>
    /// <param name="serverId">The ID of the server where the giveaway is being hosted.</param>
    /// <param name="currentChannel">The current text channel where the command is being executed.</param>
    /// <param name="guild">The guild where the giveaway is being initiated.</param>
    /// <param name="reqroles">Optional: Roles required to enter the giveaway.</param>
    /// <param name="blacklistusers">Optional: Users blacklisted from entering the giveaway.</param>
    /// <param name="blacklistroles">Optional: Roles blacklisted from entering the giveaway.</param>
    /// <param name="interaction">Optional: The Discord interaction related to the giveaway.</param>
    /// <param name="banner">Optional: The URL of the banner for the giveaway.</param>
    /// <param name="pingROle">Optional: The role to ping for the giveaway.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task GiveawaysInternal(ITextChannel chan, TimeSpan ts, string item, int winners, ulong host,
        ulong serverId, ITextChannel currentChannel, IGuild guild, string? reqroles = null,
        string? blacklistusers = null,
        string? blacklistroles = null, IDiscordInteraction? interaction = null, string banner = null,
        IRole pingROle = null)
    {
        var gconfig = await guildSettings.GetGuildConfig(serverId).ConfigureAwait(false);
        IRole role = null;
        if (gconfig.GiveawayPingRole != 0)
        {
            role = guild.GetRole(gconfig.GiveawayPingRole);
        }

        if (pingROle is not null)
        {
            role = pingROle;
        }

        var hostuser = await guild.GetUserAsync(host).ConfigureAwait(false);
        var emote = (await GetGiveawayEmote(guild.Id)).ToIEmote();
        var eb = new EmbedBuilder
        {
            Color = Mewdeko.OkColor,
            Title = item,
            Description =
                $"React with {emote} to enter!\nHosted by {hostuser.Mention}\nEnd Time: <t:{DateTime.UtcNow.Add(ts).ToUnixEpochDate()}:R> (<t:{DateTime.UtcNow.Add(ts).ToUnixEpochDate()}>)\n",
            Footer = new EmbedFooterBuilder()
                .WithText($"{winners} Winners | {guild} Giveaways | Ends on {DateTime.UtcNow.Add(ts):dd.MM.yyyy}")
        };
        if (!string.IsNullOrEmpty(reqroles))
        {
            var splitreqs = reqroles.Split(" ");
            var reqrolesparsed = new List<IRole>();
            foreach (var i in splitreqs)
            {
                if (!ulong.TryParse(i, out var parsed)) continue;
                try
                {
                    reqrolesparsed.Add(guild.GetRole(parsed));
                }
                catch
                {
                    //ignored
                }
            }

            if (reqrolesparsed.Count > 0)
            {
                eb.WithDescription(
                    $"React with {emote} to enter!\nHosted by {hostuser.Mention}\nRequired Roles: {string.Join("\n", reqrolesparsed.Select(x => x.Mention))}\nEnd Time: <t:{DateTime.UtcNow.Add(ts).ToUnixEpochDate()}:R> (<t:{DateTime.UtcNow.Add(ts).ToUnixEpochDate()}>)\n");
            }
        }

        if (!string.IsNullOrEmpty(gconfig.GiveawayEmbedColor))
        {
            var colorStr = gconfig.GiveawayEmbedColor;

            if (colorStr.StartsWith("#"))
                eb.WithColor(new Color(Convert.ToUInt32(colorStr.Replace("#", ""), 16)));
            else if (colorStr.StartsWith("0x") && colorStr.Length == 8)
                eb.WithColor(new Color(Convert.ToUInt32(colorStr.Replace("0x", ""), 16)));
            else if (colorStr.Length == 6 && IsHex(colorStr))
                eb.WithColor(new Color(Convert.ToUInt32(colorStr, 16)));
            else if (uint.TryParse(colorStr, out var colorNumber))
                eb.WithColor(new Color(colorNumber));
        }

        if (!string.IsNullOrEmpty(gconfig.GiveawayBanner))
        {
            if (Uri.IsWellFormedUriString(gconfig.GiveawayBanner, UriKind.Absolute))
                eb.WithImageUrl(gconfig.GiveawayBanner);
        }

        if (!string.IsNullOrEmpty(banner))
        {
            if (Uri.IsWellFormedUriString(banner, UriKind.Absolute))
                eb.WithImageUrl(banner);
        }

        var msg = await chan.SendMessageAsync(role is not null ? role.Mention : "", embed: eb.Build())
            .ConfigureAwait(false);
        await msg.AddReactionAsync(emote).ConfigureAwait(false);
        var time = DateTime.UtcNow + ts;
        var rem = new Database.Models.Giveaways
        {
            ChannelId = chan.Id,
            UserId = host,
            ServerId = serverId,
            Ended = 0,
            When = time,
            Item = item,
            MessageId = msg.Id,
            Winners = winners,
            Emote = emote.ToString()
        };
        if (!string.IsNullOrWhiteSpace(reqroles))
            rem.RestrictTo = reqroles;


        await using (dbContext.ConfigureAwait(false))
        {
            dbContext.Giveaways.Add(rem);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        if (interaction is not null)
            await interaction.SendConfirmFollowupAsync($"Giveaway started in {chan.Mention}").ConfigureAwait(false);
        else
            await currentChannel.SendConfirmAsync($"Giveaway started in {chan.Mention}").ConfigureAwait(false);
        return;

        bool IsHex(string value)
        {
            return value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
        }
    }


    /// <summary>
    /// Performs actions for a giveaway, such as selecting winners and notifying participants. Used for ending and rerolling giveaways.
    /// </summary>
    /// <param name="r">The giveaway to perform actions for.</param>
    /// <param name="inputguild">The guild where the giveaway is being conducted.</param>
    /// <param name="inputchannel">The text channel where the giveaway is being conducted.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task GiveawayTimerAction(Database.Models.Giveaways r, IGuild? inputguild = null,
        ITextChannel? inputchannel = null)
    {
        var dclient = client as IDiscordClient;
        var guild = inputguild ?? await dclient.GetGuildAsync(r.ServerId);
        if (guild is null)
            return;

        var channel = inputchannel ?? await guild.GetTextChannelAsync(r.ChannelId);
        if (channel is null)
            return;
        IUserMessage ch;
        try
        {
            if (await channel.GetMessageAsync(r.MessageId) is not
                IUserMessage ch1)
            {
                return;
            }

            ch = ch1;
        }
        catch
        {
            return;
        }

        var prefix = await guildSettings.GetPrefix(guild.Id).ConfigureAwait(false);


        var emote = r.Emote.ToIEmote();
        if (emote.Name == null)
        {
            await ch.Channel
                .SendErrorAsync($"[This Giveaway]({ch.GetJumpUrl()}) failed because the emote used for it is invalid!",
                    config)
                .ConfigureAwait(false);
            return;
        }

        var reacts = await ch.GetReactionUsersAsync(emote, 999999).FlattenAsync().ConfigureAwait(false);
        if (!reacts.Any())
        {
            var emoteTest = await GetGiveawayEmote(guild.Id);
            var emoteTest2 = emoteTest.ToIEmote();
            if (emoteTest2.Name == null)
            {
                await ch.Channel
                    .SendErrorAsync(
                        $"[This Giveaway]({ch.GetJumpUrl()}) failed because the emote used for it is invalid!", config)
                    .ConfigureAwait(false);
                return;
            }

            reacts = await ch.GetReactionUsersAsync(emoteTest.ToIEmote(), 999999).FlattenAsync().ConfigureAwait(false);
        }

        if (reacts.Count(x => !x.IsBot) - 1 < r.Winners)
        {
            var eb = new EmbedBuilder
            {
                Color = Mewdeko.ErrorColor, Description = "There were not enough participants!"
            };
            await ch.ModifyAsync(x =>
            {
                x.Embed = eb.Build();
                x.Content = null;
            }).ConfigureAwait(false);
            r.Ended = 1;
            dbContext.Giveaways.Update(r);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            if (r.Winners == 1)
            {
                var users = reacts.Where(x => !x.IsBot).Select(x => guild.GetUserAsync(x.Id).GetAwaiter().GetResult())
                    .Where(x => x is not null).ToList();
                if (r.RestrictTo is not null)
                {
                    var parsedreqs = new List<ulong>();
                    foreach (var i in r.RestrictTo.Split(" "))
                    {
                        if (ulong.TryParse(i, out var parsed))
                        {
                            parsedreqs.Add(parsed);
                        }
                    }

                    try
                    {
                        if (parsedreqs.Count > 0)
                        {
                            users = users.Where(x =>
                                    x.GetRoles().Any() &&
                                    x.GetRoles().Select(i => i.Id).Intersect(parsedreqs).Count() == parsedreqs.Count)
                                .ToList();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e.ToString());
                        return;
                    }
                }

                if (users.Count == 0)
                {
                    var eb1 = new EmbedBuilder().WithErrorColor()
                        .WithDescription(
                            "Looks like nobody that actually met the role requirements joined..")
                        .Build();
                    await ch.ModifyAsync(x =>
                    {
                        x.Embed = eb1;
                        x.Content = null;
                    }).ConfigureAwait(false);
                    return;
                }

                var rand = new Random();
                var index = rand.Next(users.Count);
                var user = users.ToList()[index];
                var gset = await guildSettings.GetGuildConfig(guild.Id);
                if (gset.DmOnGiveawayWin)
                {
                    if (!string.IsNullOrEmpty(gset.GiveawayEndMessage))
                    {
                        var rep = new ReplacementBuilder()
                            .WithChannel(channel)
                            .WithClient(client)
                            .WithServer(client, guild as SocketGuild)
                            .WithUser(user);

                        rep.WithOverride("%messagelink%",
                            () => $"https://discord.com/channels/{guild.Id}/{channel.Id}/{ch.Id}");
                        rep.WithOverride("%giveawayitem%", () => r.Item);
                        rep.WithOverride("%giveawaywinners%", () => r.Winners.ToString());

                        var replacer = rep.Build();

                        if (SmartEmbed.TryParse(replacer.Replace(gset.GiveawayEndMessage), guild.Id, out var embeds,
                                out var plaintext, out var components))
                        {
                            try
                            {
                                await user.SendMessageAsync(plaintext, embeds: embeds ?? null,
                                    components: components?.Build());
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                        else
                        {
                            var ebdm = new EmbedBuilder()
                                .WithOkColor()
                                .WithDescription(
                                    $"Congratulations! You won a giveaway for [{r.Item}](https://discord.com/channels/{r.ServerId}/{r.ChannelId}/{r.MessageId})!");
                            ebdm.AddField("Message from Host", replacer.Replace(gset.GiveawayEndMessage));
                            try
                            {
                                await user.SendMessageAsync(embed: ebdm.Build());
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            var ebdm = new EmbedBuilder()
                                .WithOkColor()
                                .WithDescription(
                                    $"Congratulations! You won a giveaway for [{r.Item}](https://discord.com/channels/{r.ServerId}/{r.ChannelId}/{r.MessageId})!");
                            await user.SendMessageAsync(embed: ebdm.Build());
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                var winbed = ch.Embeds.FirstOrDefault().ToEmbedBuilder()
                    .WithErrorColor()
                    .WithDescription($"Winner: {user.Mention}!\nHosted by: <@{r.UserId}>")
                    .WithFooter($"Ended at {DateTime.UtcNow:dd.MM.yyyy HH:mm:ss}");

                await ch.ModifyAsync(x =>
                {
                    x.Embed = winbed.Build();
                    x.Content = $"{r.Emote} **Giveaway Ended!** {r.Emote}";
                }).ConfigureAwait(false);
                await ch.Channel.SendMessageAsync($"Congratulations to {user.Mention}! {r.Emote}",
                    embed: new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(
                            $"{user.Mention} won the giveaway for [{r.Item}](https://discord.com/channels/{r.ServerId}/{r.ChannelId}/{r.MessageId})! \n\n- (Hosted by: <@{r.UserId}>)\n- Reroll: `{prefix}reroll {r.MessageId}`")
                        .Build()).ConfigureAwait(false);
                r.Ended = 1;
                dbContext.Giveaways.Update(r);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }
            else
            {
                var rand = new Random();
                var users = (await Task.WhenAll(reacts.Where(x => !x.IsBot)
                    .Select(x => guild.GetUserAsync(x.Id)).Where(x => x is not null))).ToHashSet();
                if (r.RestrictTo is not null)
                {
                    var parsedreqs = new List<ulong>();
                    var split = r.RestrictTo.Split(" ");
                    foreach (var i in split)
                    {
                        if (ulong.TryParse(i, out var parsed))
                        {
                            parsedreqs.Add(parsed);
                        }
                    }

                    try
                    {
                        if (parsedreqs.Count > 0)
                        {
                            users = users.Where(x =>
                                    x.GetRoles().Select(i => i.Id).Intersect(parsedreqs).Count() == parsedreqs.Count)
                                .ToHashSet();
                        }
                    }
                    catch
                    {
                        return;
                    }
                }

                if (!users.Any())
                {
                    var eb1 = new EmbedBuilder().WithErrorColor()
                        .WithDescription(
                            "Looks like nobody that actually met the role requirements joined..")
                        .Build();
                    await ch.ModifyAsync(x =>
                    {
                        x.Embed = eb1;
                        x.Content = null;
                    }).ConfigureAwait(false);
                }

                var winners = users.ToList().OrderBy(_ => rand.Next()).Take(r.Winners);

                var winbed = ch.Embeds.FirstOrDefault().ToEmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(
                        $"Winner: {string.Join(", ", winners.Take(5).Select(x => x.Mention))}!\nHosted by: <@{r.UserId}>")
                    .WithFooter($"Ended at {DateTime.UtcNow:dd.MM.yyyy HH:mm:ss}");

                await ch.ModifyAsync(x =>
                {
                    x.Embed = winbed.Build();
                    x.Content = $"{r.Emote} **Giveaway Ended!** {r.Emote}";
                }).ConfigureAwait(false);

                foreach (var winners2 in winners.Chunk(50))
                {
                    await ch.Channel.SendMessageAsync(
                        $"Congratulations to {string.Join(", ", winners2.Select(x => x.Mention))}! {r.Emote}",
                        embed: new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription(
                                $"{string.Join(", ", winners2.Select(x => x.Mention))} won the giveaway for [{r.Item}](https://discord.com/channels/{r.ServerId}/{r.ChannelId}/{r.MessageId})! \n\n- (Hosted by: <@{r.UserId}>)\n- Reroll: `{prefix}reroll {r.MessageId}`")
                            .Build()).ConfigureAwait(false);
                }

                r.Ended = 1;
                dbContext.Giveaways.Update(r);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}