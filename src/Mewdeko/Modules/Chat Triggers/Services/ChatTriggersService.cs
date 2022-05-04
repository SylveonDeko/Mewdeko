using Discord;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Common.Yml;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Chat_Triggers.Common;
using Mewdeko.Modules.Chat_Triggers.Extensions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mewdeko.Modules.Chat_Triggers.Services;

public sealed class ChatTriggersService : IEarlyBehavior, INService, IReadyExecutor
{
    public enum CtField
    {
        AutoDelete,
        DmResponse,
        AllowTarget,
        ContainsAnywhere,
        Message,
        ReactToTrigger,
        NoRespond,
        PermsEnabledByDefault,
        ChannelsEnabledByDefault
    }

    private const string MENTION_PH = "%bot.mention%";

    private const string PREPEND_EXPORT =
        @"# Keys are triggers, Each key has a LIST of custom reactions in the following format:
# - res: Response string
#   react: 
#     - <List
#     -  of
#     - reactions>
#   at: Whether custom reaction allows targets (see .h .crat) 
#   ca: Whether custom reaction expects trigger anywhere (see .h .crca) 
#   dm: Whether custom reaction DMs the response (see .h .crdm) 
#   ad: Whether custom reaction automatically deletes triggering message (see .h .crad) 
#   rtt: Whether custom reaction emotes are added to the response or trigger

";

    private static readonly ISerializer _exportSerializer = new SerializerBuilder()
        .WithEventEmitter(args => new MultilineScalarFlowStyleEmitter(args))
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithIndentedSequences()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .DisableAliases()
        .Build();

    private readonly Mewdeko _bot;
    private readonly DiscordSocketClient _client;
    private readonly CommandHandler _cmd;
    private readonly CmdCdService _cmdCds;
    private readonly TypedKey<bool> _crsReloadedKey = new("crs.reloaded");

    private readonly DbService _db;

    private readonly TypedKey<Database.Models.ChatTriggers> _gcrAddedKey = new("gcr.added");
    private readonly TypedKey<int> _gcrDeletedkey = new("gcr.deleted");
    private readonly TypedKey<Database.Models.ChatTriggers> _gcrEditedKey = new("gcr.edited");

    private readonly object _gcrWriteLock = new();
    private readonly GlobalPermissionService _gperm;
    private readonly PermissionService _perms;
    private readonly IPubSub _pubSub;
    private readonly Random _rng;
    private readonly IBotStrings _strings;

    // it is perfectly fine to have global customreactions as an array
    // 1. custom reactions are almost never added (compared to how many times they are being looped through)
    // 2. only need write locks for this as we'll rebuild+replace the array on every edit
    // 3. there's never many of them (at most a thousand, usually < 100)
    private Database.Models.ChatTriggers[] globalReactions;
    private ConcurrentDictionary<ulong, Database.Models.ChatTriggers[]> newGuildReactions;

    private bool ready;

    public ChatTriggersService(PermissionService perms, DbService db, IBotStrings strings,
        Mewdeko bot,
        DiscordSocketClient client, CommandHandler cmd, GlobalPermissionService gperm, CmdCdService cmdCds,
        IPubSub pubSub)
    {
        _db = db;
        _client = client;
        _perms = perms;
        _cmd = cmd;
        _strings = strings;
        _bot = bot;
        _cmdCds = cmdCds;
        _gperm = gperm;
        _pubSub = pubSub;
        _rng = new MewdekoRandom();

        _pubSub.Sub(_crsReloadedKey, OnCrsShouldReload);
        pubSub.Sub(_gcrAddedKey, OnGcrAdded);
        pubSub.Sub(_gcrDeletedkey, OnGcrDeleted);
        pubSub.Sub(_gcrEditedKey, OnGcrEdited);

        bot.JoinedGuild += OnJoinedGuild;
        _client.LeftGuild += OnLeftGuild;
    }

    public int Priority => -1;
    public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;


    public async Task<bool> RunBehavior(DiscordSocketClient client, IGuild guild, IUserMessage msg)
    {
        // maybe this message is a custom reaction
        var cr = TryGetChatTriggers(msg);

        if (cr is null)
            return false;
        if (await _cmdCds.TryBlock(guild, msg.Author, cr.Trigger))
            return false;

        try
        {
            if (_gperm.BlockedModules.Contains("ActualChatTriggers")) return true;

            if (guild is SocketGuild sg)
            {
                var pc = _perms.GetCacheFor(guild.Id);
                if (!pc.Permissions.CheckPermissions(msg, cr.Trigger, "ActualChatTriggers",
                        out var index))
                {
                    if (pc.Verbose)
                    {
                        var returnMsg = _strings.GetText("perm_prevent", sg.Id,
                            index + 1,
                            Format.Bold(pc.Permissions[index].GetCommand(_cmd.GetPrefix(guild), sg)));
                        try
                        {
                            await msg.Channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        Log.Information(returnMsg);
                    }

                    return true;
                }
            }

            IUserMessage sentMsg = null;
            if (!cr.NoRespond)
                sentMsg = await cr.Send(msg, _client, false).ConfigureAwait(false);

            var reactions = cr.GetReactions();
            foreach (var reaction in reactions)
            {
                try
                {
                    if (!cr.ReactToTrigger && !cr.NoRespond)
                        await sentMsg.AddReactionAsync(reaction.ToIEmote());
                    else
                        await msg.AddReactionAsync(reaction.ToIEmote());
                }
                catch
                {
                    Log.Warning("Unable to add reactions to message {Message} in server {GuildId}", sentMsg.Id,
                        cr.GuildId);
                    break;
                }

                await Task.Delay(1000);
            }

            try
            {
                if (cr.AutoDeleteTrigger)
                    await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            if (cr.GuildId is not null && msg is IUserMessage userMessage && msg.Author is IGuildUser guildUser)
            {
                var effectedUsers = cr.RoleGrantType switch
                {
                    CTRoleGrantType.Mentioned => msg.Content.GetUserMentions().Take(5),
                    CTRoleGrantType.Sender => new List<ulong>() {msg.Author.Id},
                    CTRoleGrantType.Both => msg.Content.GetUserMentions().Take(4).Append(msg.Author.Id)
                };

                foreach (var userId in effectedUsers)
                {
                    var user = await guildUser.Guild.GetUserAsync(userId);
                    try
                    {
                        var baseRoles = user.RoleIds.Where(x => x != guild?.EveryoneRole.Id).ToList();
                        var roles = baseRoles.Where(x => !cr.RemovedRoles?.Contains(x.ToString()) ?? true).ToList();
                        roles.AddRange(cr.GetGrantedRoles());
                        
                        // difference is caused by @everyone
                        if (baseRoles.Any(x => !roles.Contains(x)) || roles.Any(x => !baseRoles.Contains(x)))
                            await user.ModifyAsync(x => x.RoleIds = new(roles));
                    }
                    catch
                    {
                        Log.Warning("Unable to modify the roles of {User} in {GuildId}", guildUser.Id, cr.GuildId);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex.Message);
        }

        return false;
    }

    public string ExportCrs(ulong? guildId)
    {
        var crs = GetChatTriggersFor(guildId);

        var crsDict = crs
            .GroupBy(x => x.Trigger)
            .ToDictionary(x => x.Key, x => x.Select(ExportedTriggers.FromModel));

        return PREPEND_EXPORT + _exportSerializer
            .Serialize(crsDict)
            .UnescapeUnicodeCodePoints();
    }

    public async Task<bool> ImportCrsAsync(IGuildUser user, string input)
    {
        Dictionary<string, List<ExportedTriggers>> data;
        try
        {
            data = Yaml.Deserializer.Deserialize<Dictionary<string, List<ExportedTriggers>>>(input);
            if (data.Sum(x => x.Value.Count) == 0)
                return false;
        }
        catch
        {
            return false;
        }

        await using var uow = _db.GetDbContext();
        List<Database.Models.ChatTriggers> triggers = new();
        foreach (var (trigger, value) in data)
        {
            triggers.AddRange(value
                                    .Where(ct => !string.IsNullOrWhiteSpace(ct.Res))
                                    .Select(ct => new Database.Models.ChatTriggers
                                    {
                                        GuildId = user.Guild.Id,
                                        Response = ct.Res,
                                        Reactions = ct.React?.JoinWith("@@@"),
                                        Trigger = trigger,
                                        AllowTarget = ct.At,
                                        ContainsAnywhere = ct.Ca,
                                        DmResponse = ct.Dm,
                                        AutoDeleteTrigger = ct.Ad,
                                        NoRespond = ct.Nr,
                                        IsRegex = ct.Rgx,
                                        GrantedRoles = string.Join("@@@", ct.aRole.Select(x => x.ToString())),
                                        RemovedRoles = string.Join("@@@", ct.rRole.Select(x => x.ToString()))
                                    }));
        }

        List<ulong> roles = new();
        triggers.ForEach(x => roles.AddRange(x.GetGrantedRoles()));
        triggers.ForEach(x => roles.AddRange(x.GetRemovedRoles()));

        if (roles.Any() && !roles.Any(y => !user.Guild.GetRole(y).CanManageRole(user)))
            return false;

        await uow.ChatTriggers.AddRangeAsync(triggers);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await TriggerReloadChatTriggers();
        return true;
    }

    private async Task ReloadInternal(IReadOnlyList<ulong> allGuildIds)
    {
        await using var uow = _db.GetDbContext();
        var guildItems = await uow.ChatTriggers
            .AsNoTracking()
            .Where(x => allGuildIds.Contains(x.GuildId.Value))
            .ToListAsync();

        newGuildReactions = guildItems
            .GroupBy(k => k.GuildId!.Value)
            .ToDictionary(g => g.Key,
                g => g.Select(x =>
                {
                    x.Trigger = x.Trigger.Replace(MENTION_PH, _client.CurrentUser.Mention);
                    return x;
                }).ToArray())
            .ToConcurrent();

        lock (_gcrWriteLock)
        {
            var globalItems =
                uow.ChatTriggers
                .AsNoTracking()
                .Where(x => x.GuildId == null || x.GuildId == 0)
                .AsEnumerable()
                .Select(x =>
                {
                    x.Trigger = x.Trigger.Replace(MENTION_PH, _client.CurrentUser.Mention);
                    return x;
                })
                .ToArray();

            globalReactions = globalItems;
        }

        ready = true;
    }

    private Database.Models.ChatTriggers TryGetChatTriggers(IUserMessage umsg)
    {
        if (!ready)
            return null;

        if (umsg.Channel is not SocketTextChannel channel)
            return null;

        var content = umsg.Content.Trim().ToLowerInvariant();

        if (newGuildReactions.TryGetValue(channel.Guild.Id, out var reactions) && reactions.Length > 0)
        {
            var cr = MatchChatTriggers(content, reactions);
            if (cr is not null)
                return cr;
        }

        var localGrs = globalReactions;

        return MatchChatTriggers(content, localGrs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Database.Models.ChatTriggers MatchChatTriggers(string content, Database.Models.ChatTriggers[] crs)
    {
        var result = new List<Database.Models.ChatTriggers>(1);
        for (var i = 0; i < crs.Length; i++)
        {
            var cr = crs[i];
            var trigger = cr.Trigger;

            // regex triggers are only checked on regex content
            if (cr.IsRegex)
            {
                if (Regex.IsMatch(new string(content), trigger, RegexOptions.None, TimeSpan.FromMilliseconds(1)))
                    result.Add(cr);
                continue;
            }
            
            // if the trigger depends on user mentions to grant roles
            // those should be removed
            if (cr.RoleGrantType is CTRoleGrantType.Mentioned or CTRoleGrantType.Both)
            {
                content = content.RemoveUserMentions().Trim();
            }
            
            if (content.Length > trigger.Length)
            {
                // if input is greater than the trigger, it can only work if:
                // it has CA enabled
                if (cr.ContainsAnywhere)
                {
                    // if ca is enabled, we have to check if it is a word within the content
                    var wp = Extensions.Extensions.GetWordPosition(content, trigger);

                    // if it is, then that's valid
                    if (wp != WordPosition.None) result.Add(cr);

                    // if it's not, then it cant' work under any circumstance,
                    // because content is greater than the trigger length
                    // so it can't be equal, and it's not contained as a word
                    continue;
                }

                // if CA is disabled, and CR has AllowTarget, then the
                // content has to start with the trigger followed by a space
                if (cr.AllowTarget && content.StartsWith(trigger, StringComparison.OrdinalIgnoreCase)
                                   && content[trigger.Length] == ' ')
                    result.Add(cr);
            }
            else if (content.Length < cr.Trigger.Length)
            {
                // if input length is less than trigger length, it means
                // that the reaction can never be triggered
            }
            else
            {
                // if input length is the same as trigger length
                // reaction can only trigger if the strings are equal
                if (content.SequenceEqual(cr.Trigger)) result.Add(cr);
            }
        }

        if (result.Count == 0)
            return null;

        return result[_rng.Next(0, result.Count)];
    }

    public async Task ResetCrReactions(ulong? maybeGuildId, int id)
    {
        Database.Models.ChatTriggers cr;
        await using var uow = _db.GetDbContext();
        cr = uow.ChatTriggers.GetById(id);
        if (cr is null)
            return;

        cr.Reactions = string.Empty;

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    private Task UpdateInternalAsync(ulong? maybeGuildId, Database.Models.ChatTriggers cr)
    {
        if (maybeGuildId is { } guildId)
            UpdateInternal(guildId, cr);
        else
            return _pubSub.Pub(_gcrEditedKey, cr);

        return Task.CompletedTask;
    }

    private void UpdateInternal(ulong? maybeGuildId, Database.Models.ChatTriggers cr)
    {
        if (maybeGuildId is { } guildId)
            newGuildReactions.AddOrUpdate(guildId, new[] { cr },
                (_, old) =>
                {
                    var newArray = old.ToArray();
                    for (var i = 0; i < newArray.Length; i++)
                        if (newArray[i].Id == cr.Id)
                            newArray[i] = cr;
                    return newArray;
                });
        else
            lock (_gcrWriteLock)
            {
                var crs = globalReactions;
                for (var i = 0; i < crs.Length; i++)
                    if (crs[i].Id == cr.Id)
                        crs[i] = cr;
            }
    }

    private Task AddInternalAsync(ulong? maybeGuildId, Database.Models.ChatTriggers cr)
    {
        // only do this for perf purposes
        cr.Trigger = cr.Trigger.Replace(MENTION_PH, _client.CurrentUser.Mention);

        if (maybeGuildId is { } guildId)
            newGuildReactions.AddOrUpdate(guildId,
                new[] { cr },
                (_, old) => old.With(cr));
        else
            return _pubSub.Pub(_gcrAddedKey, cr);

        return Task.CompletedTask;
    }

    private Task DeleteInternalAsync(ulong? maybeGuildId, int id)
    {
        if (maybeGuildId is { } guildId)
        {
            newGuildReactions.AddOrUpdate(guildId,
                Array.Empty<Database.Models.ChatTriggers>(),
                (_, old) => DeleteInternal(old, id));

            return Task.CompletedTask;
        }

        lock (_gcrWriteLock)
        {
            var cr = Array.Find(globalReactions, item => item.Id == id);
            if (cr is not null) return _pubSub.Pub(_gcrDeletedkey, cr.Id);
        }

        return Task.CompletedTask;
    }

    private static Database.Models.ChatTriggers[] DeleteInternal(IReadOnlyList<Database.Models.ChatTriggers>? crs, int id)
    {
        if (crs is null || crs.Count == 0)
            return crs as Database.Models.ChatTriggers[] ?? crs?.ToArray();

        var newCrs = new Database.Models.ChatTriggers[crs.Count - 1];
        for (int i = 0, k = 0; i < crs.Count; i++, k++)
        {
            if (crs[i].Id == id)
            {
                k--;
                continue;
            }

            newCrs[k] = crs[i];
        }

        return newCrs;
    }

    public async Task SetCrReactions(ulong? guildId, int id, IEnumerable<string> emojis)
    {
        Database.Models.ChatTriggers cr;
        await using (var uow = _db.GetDbContext())
        {
            cr = uow.ChatTriggers.GetById(id);
            if (cr is null)
                return;

            cr.Reactions = string.Join("@@@", emojis);

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        await UpdateInternalAsync(guildId, cr);
    }

    public async Task<(bool Sucess, bool NewValue)> ToggleCrOptionAsync(Database.Models.ChatTriggers ct, CtField field)
    {
        var newVal = false;
        await using (var uow = _db.GetDbContext())
        {
            if (ct is null)
                return (false, false);
            newVal = field switch
            {
                CtField.AutoDelete => ct.AutoDeleteTrigger = !ct.AutoDeleteTrigger,
                CtField.ContainsAnywhere => ct.ContainsAnywhere = !ct.ContainsAnywhere,
                CtField.DmResponse => ct.DmResponse = !ct.DmResponse,
                CtField.AllowTarget => ct.AllowTarget = !ct.AllowTarget,
                CtField.ReactToTrigger => ct.ReactToTrigger = !ct.ReactToTrigger,
                CtField.NoRespond => ct.NoRespond = !ct.NoRespond,
                _ => newVal
            };

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        await UpdateInternalAsync(ct.GuildId, ct);

        return (true, newVal);
    }

    public Database.Models.ChatTriggers GetChatTriggers(ulong? guildId, int id)
    {
        using var uow = _db.GetDbContext();
        var cr = uow.ChatTriggers.GetById(id);
        if (cr == null || cr.GuildId != guildId)
            return null;

        return cr;
    }

    public int DeleteAllChatTriggers(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var count = uow.ChatTriggers.ClearFromGuild(guildId);
        uow.SaveChanges();

        newGuildReactions.TryRemove(guildId, out _);

        return count;
    }

    public bool ReactionExists(ulong? guildId, string input)
    {
        using var uow = _db.GetDbContext();
        var cr = uow.ChatTriggers.GetByGuildIdAndInput(guildId, input);
        return cr != null;
    }

    public Task OnReadyAsync() => ReloadInternal(_bot.GetCurrentGuildIds());

    private ValueTask OnCrsShouldReload(bool _) => new(ReloadInternal(_bot.GetCurrentGuildIds()));

    private ValueTask OnGcrAdded(Database.Models.ChatTriggers c)
    {
        lock (_gcrWriteLock)
        {
            var newGlobalReactions = new Database.Models.ChatTriggers[globalReactions.Length + 1];
            Array.Copy(globalReactions, newGlobalReactions, globalReactions.Length);
            newGlobalReactions[globalReactions.Length] = c;
            globalReactions = newGlobalReactions;
        }

        return default;
    }

    private ValueTask OnGcrEdited(Database.Models.ChatTriggers c)
    {
        lock (_gcrWriteLock)
        {
            for (var i = 0; i < globalReactions.Length; i++)
                if (globalReactions[i].Id == c.Id)
                {
                    globalReactions[i] = c;
                    return default;
                }

            // if edited cr is not found?!
            // add it
            OnGcrAdded(c);
        }

        return default;
    }

    private ValueTask OnGcrDeleted(int id)
    {
        lock (_gcrWriteLock)
        {
            var newGlobalReactions = DeleteInternal(globalReactions, id);
            globalReactions = newGlobalReactions;
        }

        return default;
    }

    public Task TriggerReloadChatTriggers() => _pubSub.Pub(_crsReloadedKey, true);

    private Task OnLeftGuild(SocketGuild arg)
    {
        newGuildReactions.TryRemove(arg.Id, out _);

        return Task.CompletedTask;
    }

    private async Task OnJoinedGuild(GuildConfig gc)
    {
        await using var uow = _db.GetDbContext();
        var crs = await
            uow
            .ChatTriggers
            .AsNoTracking()
            .Where(x => x.GuildId == gc.GuildId)
            .ToArrayAsync();

        newGuildReactions[gc.GuildId] = crs;
    }


    public async Task<Database.Models.ChatTriggers> AddAsync(ulong? guildId, string key, string message, bool regex)
    {
        key = key.ToLowerInvariant();
        var cr = new Database.Models.ChatTriggers
        {
            GuildId = guildId,
            Trigger = key,
            Response = message,
            IsRegex = regex
        };

        if (cr.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
            cr.AllowTarget = true;

        await using (var uow = _db.GetDbContext())
        {
            uow.ChatTriggers.Add(cr);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        await AddInternalAsync(guildId, cr).ConfigureAwait(false);

        return cr;
    }

    public async Task<Database.Models.ChatTriggers> EditAsync(ulong? guildId, int id, string message, bool? regex)
    {
        await using var uow = _db.GetDbContext();
        var cr = uow.ChatTriggers.GetById(id);

        if (cr == null || cr.GuildId != guildId)
            return null;

        cr.IsRegex = regex ?? cr.IsRegex;

        // disable allowtarget if message had target, but it was removed from it
        if (!message.Contains("%target%", StringComparison.OrdinalIgnoreCase)
            && cr.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
            cr.AllowTarget = false;

        cr.Response = message;

        // enable allow target if message is edited to contain target
        if (cr.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
            cr.AllowTarget = true;

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, cr);

        return cr;
    }
    
    public async Task<Database.Models.ChatTriggers> DeleteAsync(ulong? guildId, int id)
    {
        await using var uow = _db.GetDbContext();
        var toDelete = uow.ChatTriggers.GetById(id);

        if (toDelete is null)
            return null;

        if ((toDelete.IsGlobal() && guildId == null) || guildId == toDelete.GuildId)
        {
            uow.ChatTriggers.Remove(toDelete);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await DeleteInternalAsync(guildId, id);
            return toDelete;
        }

        return null;
    }

    public async Task<Database.Models.ChatTriggers> SetRoleGrantType(ulong? guildId, int id, CTRoleGrantType type)
    {
        await using var uow = _db.GetDbContext();
        var cr = uow.ChatTriggers.GetById(id);

        if (cr == null || cr.GuildId != guildId)
            return null;

        cr.RoleGrantType = type;

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, cr);

        return cr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Database.Models.ChatTriggers[] GetChatTriggersFor(ulong? maybeGuildId)
    {
        if (maybeGuildId is { } guildId)
            return newGuildReactions.TryGetValue(guildId, out var crs)
                ? crs
                : Array.Empty<Database.Models.ChatTriggers>();
        return Array.Empty<Database.Models.ChatTriggers>();
    }

    public async Task ToggleGrantedRole(Database.Models.ChatTriggers cr, ulong rId)
    {
        await using var uow = _db.GetDbContext();
        var roles = cr.GetGrantedRoles();
        if (!roles.Contains(rId))
            roles.Add(rId);
        else
            roles.RemoveAll(x => x == rId);

        cr.GrantedRoles = string.Join("@@@", roles.Select(x => x.ToString()));
        uow.ChatTriggers.Update(cr);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(cr.GuildId, cr).ConfigureAwait(false);
    }

    public async Task ToggleRemovedRole(Database.Models.ChatTriggers cr, ulong rId)
    {
        await using var uow = _db.GetDbContext();
        var roles = cr.GetRemovedRoles();
        if (!roles.Contains(rId))
            roles.Add(rId);
        else
            roles.RemoveAll(x => x == rId);

        cr.RemovedRoles = string.Join("@@@", roles.Select(x => x.ToString()));
        uow.ChatTriggers.Update(cr);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(cr.GuildId, cr).ConfigureAwait(false);
    }

    public EmbedBuilder GetEmbed(Database.Models.ChatTriggers ct, ulong? gId = null, string? title = null)
    {
        var eb = new EmbedBuilder().WithOkColor()
            .WithTitle(title)
            .WithDescription($"#{ct.Id}")
            .AddField(efb => efb.WithName(_strings.GetText("trigger", gId)).WithValue(ct.Trigger.TrimTo(1024)))
            .AddField(efb =>
                efb.WithName(_strings.GetText("response", gId))
                    .WithValue($"{(ct.Response + "\n```css\n" + ct.Response).TrimTo(1020)}```"));
        var reactions = ct.GetReactions();
        if (reactions.Length >= 1)
            eb.AddField(_strings.GetText("trigger_reactions", gId), string.Concat(reactions));
        var addedRoles = ct.GetGrantedRoles();
        if (addedRoles.Count >= 1)
            eb.AddField(_strings.GetText("added_roles", gId), addedRoles.Select(x => $"<@&{x}>").Aggregate((x, y) => $"{x}, {y}"));
        var removedRoles = ct.GetRemovedRoles();
        if (removedRoles.Count >= 1)
            eb.AddField(_strings.GetText("removed_roles", gId), removedRoles.Select(x => $"<@&{x}>").Aggregate((x, y) => $"{x}, {y}"));
        if (addedRoles.Count >= 1 || removedRoles.Count >= 1)
            eb.AddField(_strings.GetText("role_grant_type", gId), ct.RoleGrantType);
        return eb;
    }
}
