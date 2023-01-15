using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Humanizer.Localisation;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Afk.Services;

namespace Mewdeko.Modules.Afk;

public class Afk : MewdekoModuleBase<AfkService>
{
    private readonly InteractiveService interactivity;
    private readonly DiscordSocketClient client;

    public Afk(InteractiveService serv, DiscordSocketClient client)
    {
        interactivity = serv;
        this.client = client;
    }

    public enum AfkTypeEnum
    {
        SelfDisable = 1,
        OnMessage = 2,
        OnType = 3,
        Either = 4
    }

    [Cmd, Aliases, Priority(0)]
    public async Task SetAfk([Remainder] string? message = null)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (message == null)
        {
            var afkmsg = Service.GetAfkMessage(ctx.Guild.Id, ctx.User.Id).Select(x => x.Message);
            var enumerable = afkmsg as string[] ?? afkmsg.ToArray();
            if (enumerable.Length == 0 || enumerable.Last()?.Length == 0)
            {
                await Service.AfkSet(ctx.Guild, (IGuildUser)ctx.User, "_ _", 0).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("afk_enabled_no_message").ConfigureAwait(false);
                try
                {
                    var user = await ctx.Guild.GetUserAsync(ctx.User.Id).ConfigureAwait(false);
                    var toset = user.Nickname is null
                        ? $"[AFK] {user.Username.TrimTo(26)}"
                        : $"[AFK] {user.Nickname.Replace("[AFK]", "").TrimTo(26)}";
                    await user.ModifyAsync(x => x.Nickname = toset).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }

                await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
                return;
            }

            await Service.AfkSet(ctx.Guild, (IGuildUser)ctx.User, "", 0).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("afk_disabled").ConfigureAwait(false);
            try
            {
                var user = await ctx.Guild.GetUserAsync(ctx.User.Id).ConfigureAwait(false);
                await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
            return;
        }

        if (message.Length != 0 && message.Length > await Service.GetAfkLength(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("afk_message_too_long", Service.GetAfkLength(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild, (IGuildUser)ctx.User, message.EscapeWeirdStuff(), 0).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("afk_enabled", message).ConfigureAwait(false);
        try
        {
            var user1 = await ctx.Guild.GetUserAsync(ctx.User.Id).ConfigureAwait(false);
            var toset1 = user1.Nickname is null
                ? $"[AFK] {user1.Username.TrimTo(26)}"
                : $"[AFK] {user1.Nickname.Replace("[AFK]", "").TrimTo(26)}";
            await user1.ModifyAsync(x => x.Nickname = toset1).ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
    }

    [Cmd, Aliases, Priority(0), UserPerm(GuildPermission.ManageGuild)]
    public async Task AfkDel()
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (await Service.GetAfkDel(ctx.Guild.Id) == 0)
        {
            await ReplyConfirmLocalizedAsync("afk_messages_nodelete").ConfigureAwait(false);
            return;
        }

        await ReplyConfirmLocalizedAsync("afk_messages_delete", TimeSpan.FromSeconds(await Service.GetAfkDel(ctx.Guild.Id)).Humanize(maxUnit: TimeUnit.Minute))
            .ConfigureAwait(false);
    }

    [Cmd, Aliases, Priority(1)]
    public async Task AfkDel(int num)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ctx.Channel.SendErrorAsync(
                    "Hold your horses I just started back up! Give me a few seconds then this command will be ready!\nIn the meantime check out https://mewdeko.tech/changelog for bot updates!")
                .ConfigureAwait(false);
            return;
        }

        switch (num)
        {
            case < 0:
                return;
            case 0:
                await Service.AfkDelSet(ctx.Guild, 0).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("Deletion of the Afk Message has been disabled!").ConfigureAwait(false);
                break;
            default:
                await Service.AfkDelSet(ctx.Guild, num).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync($"Afk messages will now delete after {num} seconds.").ConfigureAwait(false);
                break;
        }
    }

    [Cmd, Aliases, Priority(0)]
    public async Task TimedAfk(StoopidTime time, [Remainder] string message)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ctx.Channel.SendErrorAsync(
                    "Hold your horses I just started back up! Give me a few seconds then this command will be ready!\nIn the meantime check out https://mewdeko.tech/changelog for bot updates!")
                .ConfigureAwait(false);
            return;
        }

        if (message.Length != 0 && message.Length > await Service.GetAfkLength(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("afk_message_too_long", Service.GetAfkLength(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild, ctx.User as IGuildUser, message, 1, DateTime.UtcNow + time.Time);
        await ctx.Channel.SendConfirmAsync(
            $"Timed AFK has been set, and will unset in {TimestampTag.FromDateTimeOffset(DateTimeOffset.UtcNow + time.Time, TimestampTagStyles.Relative)}.\nMessage set to:\n{message}");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task CustomAfkMessage([Remainder] string embedCode)
    {
        if (embedCode == "-")
        {
            await Service.SetCustomAfkMessage(ctx.Guild, "-").ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Afk messages will now have the default look.").ConfigureAwait(false);
            return;
        }

        await Service.SetCustomAfkMessage(ctx.Guild, embedCode).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync("Sucessfully updated afk message!").ConfigureAwait(false);
    }

    [Cmd, Aliases, Priority(0)]
    public async Task GetActiveAfks()
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ctx.Channel.SendErrorAsync(
                    "Hold your horses I just started back up! Give me a few seconds then this command will be ready!\nIn the meantime check out https://mewdeko.tech/changelog for bot updates!")
                .ConfigureAwait(false);
            return;
        }

        var afks = await Service.GetAfkUsers(ctx.Guild).ConfigureAwait(false);
        if (afks.Length == 0)
        {
            await ctx.Channel.SendErrorAsync("There are no currently AFK users!").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(afks.ToArray().Length / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle($"{Format.Bold("Active AFKs")} - {afks.ToArray().Length}")
                .WithDescription(string.Join("\n", afks.ToArray().Skip(page * 20).Take(20)));
        }
    }

    [Cmd, Aliases, Priority(0), UserPerm(GuildPermission.ManageMessages)]
    public async Task AfkView(IGuildUser user)
    {
        if (!await CheckRoleHierarchy(user))
            return;
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ctx.Channel.SendErrorAsync(
                    "Hold your horses I just started back up! Give me a few seconds then this command will be ready!\nIn the meantime check out https://mewdeko.tech/changelog for bot updates!")
                .ConfigureAwait(false);
            return;
        }

        if (!Service.IsAfk(user.Guild, user))
        {
            await ctx.Channel.SendErrorAsync("This user isn't afk!").ConfigureAwait(false);
            return;
        }

        var msg = Service.GetAfkMessage(user.Guild.Id, user.Id).Last();
        await ctx.Channel.SendConfirmAsync($"{user}'s Afk is:\n{msg.Message}").ConfigureAwait(false);
    }

    [Cmd, Aliases, Priority(0), UserPerm(GuildPermission.ManageChannels)]
    public async Task AfkDisabledList()
    {
        var mentions = new List<string>();
        var chans = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
        if (string.IsNullOrEmpty(chans) || chans == "0")
        {
            await ctx.Channel.SendErrorAsync("You don't have any disabled Afk channels.").ConfigureAwait(false);
            return;
        }

        foreach (var i in chans.Split(","))
        {
            var role = await ctx.Guild.GetTextChannelAsync(Convert.ToUInt64(i)).ConfigureAwait(false);
            mentions.Add(role.Mention);
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(mentions.ToArray().Length / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle(
                    $"{Format.Bold("Disabled Afk Channels")} - {mentions.ToArray().Length}")
                .WithDescription(string.Join("\n", mentions.ToArray().Skip(page * 20).Take(20)));
        }
    }

    [Cmd, Aliases, Priority(0), UserPerm(GuildPermission.Administrator)]
    public async Task AfkLength(int num)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ctx.Channel.SendErrorAsync(
                    "Hold your horses I just started back up! Give me a few seconds then this command will be ready!\nIn the meantime check out https://mewdeko.tech/changelog for bot updates!")
                .ConfigureAwait(false);
            return;
        }

        if (num > 4096)
        {
            await ctx.Channel.SendErrorAsync(
                "The Maximum Length is 4096 per Discord limits. Please put a number lower than that.").ConfigureAwait(false);
        }
        else
        {
            await Service.AfkLengthSet(ctx.Guild, num).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"AFK Length Sucessfully Set To {num} Characters").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, Priority(1), UserPerm(GuildPermission.Administrator)]
    public async Task AfkType(AfkTypeEnum afkTypeEnum)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        await Service.AfkTypeSet(ctx.Guild, (int)afkTypeEnum).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("afk_type_set", afkTypeEnum).ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task AfkTimeout(StoopidTime time)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (time.Time < TimeSpan.FromSeconds(1) || time.Time > TimeSpan.FromHours(2))
        {
            await ctx.Channel.SendErrorAsync("The maximum Afk timeout is 2 Hours. Minimum is 1 Second.").ConfigureAwait(false);
            return;
        }

        await Service.AfkTimeoutSet(ctx.Guild, Convert.ToInt32(time.Time.TotalSeconds)).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"Your AFK Timeout has been set to {time.Time.Humanize()}").ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task AfkUndisable(params ITextChannel[] chan)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var mentions = new List<string>();
        var toremove = new List<string>();
        var chans = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
        if (string.IsNullOrWhiteSpace(chans) || chans == "0")
        {
            await ctx.Channel.SendErrorAsync("You don't have any disabled channels!").ConfigureAwait(false);
            return;
        }

        var e = chans.Split(",");
        var list = e.ToList();
        foreach (var i in chan)
        {
            if (e.Contains(i.Id.ToString()))
            {
                toremove.Add(i.Id.ToString());
                mentions.Add(i.Mention);
            }
        }

        if (mentions.Count == 0)
        {
            await ctx.Channel.SendErrorAsync("The channels you have specifed are not set to ignore Afk!").ConfigureAwait(false);
            return;
        }

        if (!list.Except(toremove).Any())
        {
            await Service.AfkDisabledSet(ctx.Guild, "0").ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Mewdeko will no longer ignore afk in any channel.").ConfigureAwait(false);
            return;
        }

        await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list.Except(toremove))).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync(
            $"Successfully removed the channels {string.Join(",", mentions)} from the list of ignored Afk channels.").ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task AfkDisable(params ITextChannel[] chan)
    {
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var list = new HashSet<string>();
        // ReSharper disable once CollectionNeverQueried.Local
        var newchans = new HashSet<string>();
        var mentions = new HashSet<string>();
        if (await Service.GetDisabledAfkChannels(ctx.Guild.Id) == "0" ||
            string.IsNullOrWhiteSpace(await Service.GetDisabledAfkChannels(ctx.Guild.Id)))
        {
            foreach (var i in chan)
            {
                list.Add(i.Id.ToString());
                mentions.Add(i.Mention);
            }

            await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"Afk has been disabled in the channels {string.Join(",", mentions)}").ConfigureAwait(false);
        }
        else
        {
            var e = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
            var w = e.Split(",");
            foreach (var i in w) list.Add(i);

            foreach (var i in chan)
            {
                if (!w.Contains(i.Id.ToString()))
                {
                    list.Add(i.Id.ToString());
                    mentions.Add(i.Mention);
                }

                newchans.Add(i.Id.ToString());
            }

            if (mentions.Count > 0)
            {
                await ctx.Channel.SendErrorAsync(
                    "No channels were added because the channels you specified are already in the list.").ConfigureAwait(false);
                return;
            }

            await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"Added {string.Join(",", mentions)} to the list of channels AFK ignores.").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages), Priority(0)]
    public async Task AfkRemove(params IGuildUser[] user)
    {
        foreach (var i in user)
        {
            if (!await CheckRoleHierarchy(i))
                return;
        }

        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var users = 0;
        var erroredusers = 0;
        foreach (var i in user)
        {
            var curafk = Service.IsAfk(ctx.Guild, i);
            if (!curafk)
                continue;

            if (!await CheckRoleHierarchy(i, false).ConfigureAwait(false))
            {
                erroredusers++;
                continue;
            }

            await Service.AfkSet(ctx.Guild, i, "", 0).ConfigureAwait(false);
            users++;
            try
            {
                await i.ModifyAsync(x => x.Nickname = i.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
            }
            catch
            {
                //ignored
            }
        }

        switch (users)
        {
            case > 0 when erroredusers == 0:
                await ReplyConfirmLocalizedAsync("afk_rm_multi_success", users).ConfigureAwait(false);
                break;
            case 0 when erroredusers == 0:
                await ReplyErrorLocalizedAsync("afk_rm_fail_noafk").ConfigureAwait(false);
                break;
            case > 0 when erroredusers > 0:
                await ReplyConfirmLocalizedAsync("afk_success_hierarchy", users, erroredusers).ConfigureAwait(false);
                break;
            case 0 when erroredusers >= 1:
                await ReplyErrorLocalizedAsync("afk_rm_fail_hierarchy").ConfigureAwait(false);
                break;
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages), Priority(1)]
    public async Task AfkRemove(IGuildUser user)
    {
        if (!await CheckRoleHierarchy(user))
            return;
        if (Environment.GetEnvironmentVariable($"AFK_CACHED_{client.ShardId}") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (!Service.IsAfk(ctx.Guild, user))
        {
            await ReplyErrorLocalizedAsync("afk_rm_fail_noafk").ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild, user, "", 0).ConfigureAwait(false);
        try
        {
            await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
        }
        catch
        {
            //ignored
        }

        await Service.AfkSet(ctx.Guild, user, "", 0).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("afk_rm_success", user.Mention).ConfigureAwait(false);
    }
}