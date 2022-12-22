using System.Threading.Tasks;
using Mewdeko.Common.ModuleBehaviors;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Swan;

namespace Mewdeko.Modules.Giveaways.Services;

public class GiveawayService : INService, IReadyExecutor
{
    private readonly DiscordSocketClient client;
    private readonly IBotCredentials creds;
    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;

    public GiveawayService(DiscordSocketClient client, DbService db, IBotCredentials creds,
        GuildSettingsService guildSettings)
    {
        this.client = client;
        this.db = db;
        this.creds = creds;
        this.guildSettings = guildSettings;
    }

    public async Task OnReadyAsync()
    {
        Log.Information("Giveaway Loop Started");
        while (true)
        {
            await Task.Delay(2000).ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var reminders = await GetGiveawaysBeforeAsync(now).ConfigureAwait(false);
                if (reminders.Count == 0)
                    continue;

                Log.Information($"Executing {reminders.Count} giveaways.");

                // make groups of 5, with 1.5 second inbetween each one to ensure against ratelimits
                var i = 0;
                foreach (var group in reminders
                             .GroupBy(_ => ++i / ((reminders.Count / 5) + 1)))
                {
                    var executedGiveaways = group.ToList();
                    await Task.WhenAll(executedGiveaways.Select(GiveawayTimerAction)).ConfigureAwait(false);
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

    public async Task SetGiveawayEmote(IGuild guild, string emote)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.GiveawayEmote = emote;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task<string> GetGiveawayEmote(ulong id)
        => (await guildSettings.GetGuildConfig(id)).GiveawayEmote;

    private async Task UpdateGiveaways(List<Database.Models.Giveaways> g)
    {
        await using var uow = db.GetDbContext();
        foreach (var i in g)
        {
            var toupdate = new Database.Models.Giveaways
            {
                When = i.When,
                BlacklistRoles = i.BlacklistRoles,
                BlacklistUsers = i.BlacklistUsers,
                ChannelId = i.ChannelId,
                Ended = 1,
                MessageId = i.MessageId,
                RestrictTo = i.RestrictTo,
                Item = i.Item,
                ServerId = i.ServerId,
                UserId = i.UserId,
                Winners = i.Winners
            };
            uow.Giveaways.Remove(i);
            uow.Giveaways.Add(toupdate);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    private Task<List<Database.Models.Giveaways>> GetGiveawaysBeforeAsync(DateTime now)
    {
        using var uow = db.GetDbContext();
        return uow.Giveaways
            .FromSqlInterpolated(
                $"select * from giveaways where ((serverid >> 22) % {creds.TotalShards}) == {client.ShardId} and \"when\" < {now} and \"Ended\" == 0;")
            .ToListAsync();
    }

    public async Task GiveawaysInternal(ITextChannel chan, TimeSpan ts, string item, int winners, ulong host,
        ulong serverId, ITextChannel currentChannel, IGuild guild, string? reqroles = null, string? blacklistusers = null,
        string? blacklistroles = null, IDiscordInteraction? interaction = null)
    {
        var hostuser = await guild.GetUserAsync(host).ConfigureAwait(false);
        var emote = (await GetGiveawayEmote(guild.Id)).ToIEmote();
        var eb = new EmbedBuilder
        {
            Color = Mewdeko.OkColor,
            Title = item,
            Description =
                $"React with {emote} to enter!\nHosted by {hostuser.Mention}\nEnd Time: <t:{DateTime.UtcNow.Add(ts).ToUnixEpochDate()}:R> (<t:{DateTime.UtcNow.Add(ts).ToUnixEpochDate()}>)\n",
            Footer = new EmbedFooterBuilder()
                .WithText($"{winners} Winners | Mewdeko Giveaways")
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

        var msg = await chan.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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

        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            uow.Giveaways.Add(rem);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        if (interaction is not null)
            await interaction.SendConfirmFollowupAsync($"Giveaway started in {chan.Mention}").ConfigureAwait(false);
        else
            await currentChannel.SendConfirmAsync($"Giveaway started in {chan.Mention}").ConfigureAwait(false);
    }

    public async Task GiveawayTimerAction(Database.Models.Giveaways r)
    {
        if (client.GetGuild(r.ServerId) is not { } guild)
            return;
        if (client.GetGuild(r.ServerId).GetTextChannel(r.ChannelId) is not { } channel)
            return;
        IUserMessage ch;
        try
        {
            if (await channel.GetMessageAsync(r.MessageId).ConfigureAwait(false) is not
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

        await using var uow = db.GetDbContext();
        var emote = r.Emote.ToIEmote();
        if (emote.Name == null)
        {
            await ch.Channel.SendErrorAsync($"[This Giveaway]({ch.GetJumpUrl()}) failed because the emote used for it is invalid!").ConfigureAwait(false);
        }

        var reacts = await ch.GetReactionUsersAsync(emote, 999999).FlattenAsync().ConfigureAwait(false);
        if (reacts.Count() - 1 <= r.Winners)
        {
            var eb = new EmbedBuilder
            {
                Color = Mewdeko.ErrorColor, Description = "There were not enough participants!"
            };
            await ch.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
            r.Ended = 1;
            uow.Giveaways.Update(r);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            if (r.Winners == 1)
            {
                var users = reacts.Where(x => !x.IsBot).Select(x => guild.GetUser(x.Id)).ToList();
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
                            users = users.Where(x => x.Roles.Select(i => i.Id).Intersect(parsedreqs).Count() == parsedreqs.Count)
                                .ToList();
                        }
                    }
                    catch
                    {
                        return;
                    }
                }

                if (users.Count == 0)
                {
                    var eb1 = new EmbedBuilder().WithErrorColor()
                        .WithDescription(
                            "Looks like nobody that actually met the role requirements joined..")
                        .Build();
                    await ch.ModifyAsync(x => x.Embed = eb1).ConfigureAwait(false);
                    return;
                }

                var rand = new Random();
                var index = rand.Next(users.Count);
                var user = users.ToList()[index];
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.OkColor, Description = $"{user.Mention} won the giveaway for {r.Item}!"
                };
                await ch.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
                await ch.Channel.SendMessageAsync($"{user.Mention} won the giveaway for {r.Item}!",
                    embed: new EmbedBuilder().WithOkColor().WithDescription($"[Jump To Giveaway]({ch.GetJumpUrl()})")
                        .Build()).ConfigureAwait(false);
                r.Ended = 1;
                uow.Giveaways.Update(r);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
            else
            {
                var rand = new Random();
                var users = reacts.Where(x => !x.IsBot).Select(x => guild.GetUser(x.Id)).ToList();
                if (r.RestrictTo is not null)
                {
                    var parsedreqs = new List<ulong>();
                    var split = r.RestrictTo.Split(" ");
                    Console.Write(split.Length);
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
                            users = users.Where(x => x.Roles.Select(i => i.Id).Intersect(parsedreqs).Count() == parsedreqs.Count)
                                .ToList();
                        }
                    }
                    catch
                    {
                        return;
                    }
                }

                if (users.Count == 0)
                {
                    var eb1 = new EmbedBuilder().WithErrorColor()
                        .WithDescription(
                            "Looks like nobody that actually met the role requirements joined..")
                        .Build();
                    await ch.ModifyAsync(x => x.Embed = eb1).ConfigureAwait(false);
                }

                var winners = users.ToList().OrderBy(_ => rand.Next()).Take(r.Winners);
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.OkColor, Description = $"{string.Join("", winners.Select(x => x.Mention))} won the giveaway for {r.Item}!"
                };
                await ch.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
                foreach (var winners2 in winners.Chunk(50))
                {
                    await ch.Channel.SendMessageAsync(
                        $"{string.Join("", winners2.Select(x => x.Mention))} won the giveaway for {r.Item}!",
                        embed: new EmbedBuilder().WithOkColor().WithDescription($"[Jump To Giveaway]({ch.GetJumpUrl()})")
                            .Build()).ConfigureAwait(false);
                }

                r.Ended = 1;
                uow.Giveaways.Update(r);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task GiveawayReroll(Database.Models.Giveaways r)
    {
        if (client.GetGuild(r.ServerId) is not { } guild)
            return;
        if (client.GetGuild(r.ServerId).GetTextChannel(r.ChannelId) is not { } channel)
            return;
        IUserMessage ch;
        try
        {
            if (await channel.GetMessageAsync(r.MessageId).ConfigureAwait(false) is not
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

        await using var uow = db.GetDbContext();
        var emote = r.Emote.ToIEmote();
        if (emote.Name == null)
        {
            await ch.Channel.SendErrorAsync($"[This Giveaway]({ch.GetJumpUrl()}) failed because the emote used for it is invalid!").ConfigureAwait(false);
        }

        var reacts = await ch.GetReactionUsersAsync(emote, 999999).FlattenAsync().ConfigureAwait(false);
        if (reacts.Count() - 1 <= r.Winners)
        {
            var eb = new EmbedBuilder
            {
                Color = Mewdeko.ErrorColor, Description = "There were not enough participants!"
            };
            await ch.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
            r.Ended = 1;
            uow.Giveaways.Update(r);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            if (r.Winners == 1)
            {
                var users = reacts.Where(x => !x.IsBot).Select(x => guild.GetUser(x.Id)).ToList();
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
                            users = users.Where(x => x.Roles.Select(i => i.Id).Intersect(parsedreqs).Count() == parsedreqs.Count)
                                .ToList();
                        }
                    }
                    catch
                    {
                        return;
                    }
                }

                if (users.Count == 0)
                {
                    var eb1 = new EmbedBuilder().WithErrorColor()
                        .WithDescription(
                            "Looks like nobody that actually met the role requirements joined..")
                        .Build();
                    await ch.ModifyAsync(x => x.Embed = eb1).ConfigureAwait(false);
                    return;
                }

                var rand = new Random();
                var index = rand.Next(users.Count);
                var user = users.ToList()[index];
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.OkColor, Description = $"{user.Mention} won the giveaway for {r.Item}!"
                };
                await ch.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
                await ch.Channel.SendMessageAsync($"{user.Mention} won the giveaway for {r.Item}!",
                    embed: new EmbedBuilder().WithOkColor().WithDescription($"[Jump To Giveaway]({ch.GetJumpUrl()})")
                        .Build()).ConfigureAwait(false);
                r.Ended = 1;
                uow.Giveaways.Update(r);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
            else
            {
                var rand = new Random();
                var users = reacts.Where(x => !x.IsBot).Select(x => guild.GetUser(x.Id)).ToList();
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
                            users = users.Where(x => x.Roles.Select(i => i.Id).Intersect(parsedreqs).Count() == parsedreqs.Count)
                                .ToList();
                        }
                    }
                    catch
                    {
                        return;
                    }
                }

                if (users.Count == 0)
                {
                    var eb1 = new EmbedBuilder().WithErrorColor()
                        .WithDescription(
                            "Looks like nobody that actually met the role requirements joined..")
                        .Build();
                    await ch.ModifyAsync(x => x.Embed = eb1).ConfigureAwait(false);
                }

                var winners = users.ToList().OrderBy(_ => rand.Next()).Take(r.Winners);
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.OkColor, Description = $"{string.Join("", winners.Select(x => x.Mention))} won the giveaway for {r.Item}!"
                };
                await ch.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
                foreach (var winners2 in winners.Chunk(50))
                {
                    await ch.Channel.SendMessageAsync(
                        $"{string.Join("", winners2.Select(x => x.Mention))} won the giveaway for {r.Item}!",
                        embed: new EmbedBuilder().WithOkColor().WithDescription($"[Jump To Giveaway]({ch.GetJumpUrl()})")
                            .Build()).ConfigureAwait(false);
                }

                r.Ended = 1;
                uow.Giveaways.Update(r);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}