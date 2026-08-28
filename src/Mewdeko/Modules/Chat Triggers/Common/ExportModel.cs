using DataModel;

namespace Mewdeko.Modules.Chat_Triggers.Common;

/// <summary>
///     Represents exported chat triggers.
/// </summary>
public class ExportedTriggers
{
    /// <summary>
    ///     Gets or sets the roles to be added by the trigger.
    /// </summary>
    public List<ulong> ARole = [];

    /// <summary>
    ///     Gets or sets the reactions associated with the trigger.
    /// </summary>
    public string[]? React;

    /// <summary>
    ///     Gets or sets the roles to be removed by the trigger.
    /// </summary>
    public List<ulong> RRole = [];

    // Properties for backwards compatibility with NadekoBot

    /// <summary>
    ///     Gets or sets the ID.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    ///     Gets or sets the response.
    /// </summary>
    public string Res { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether auto-delete is enabled for the trigger.
    /// </summary>
    public bool Ad { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger allows targeting.
    /// </summary>
    public bool At { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger contains anywhere.
    /// </summary>
    public bool Ca { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger has a direct message response.
    /// </summary>
    public bool Dm { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger is a regular expression.
    /// </summary>
    public bool Rgx { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger reacts to the trigger.
    /// </summary>
    public bool Rtt { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger has no response.
    /// </summary>
    public bool Nr { get; set; }

    /// <summary>
    ///     Gets or sets the type of role grant for the trigger.
    /// </summary>
    public CtRoleGrantType Rgt { get; set; }

    /// <summary>
    ///     Gets or sets the valid trigger types for the trigger.
    /// </summary>
    public ChatTriggerType VTypes { get; set; } = ChatTriggerType.Message;

    /// <summary>
    ///     Gets or sets the application command name for the trigger.
    /// </summary>
    public string AcName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the application command description for the trigger.
    /// </summary>
    public string AcDesc { get; set; } = "";

    /// <summary>
    ///     Gets or sets the application command type for the trigger.
    /// </summary>
    public CtApplicationCommandType Act { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the response is ephemeral.
    /// </summary>
    public bool Eph { get; set; }

    /// <summary>
    ///     Gets or sets the trigger text. Needed on import, since a single exported key may hold several triggers.
    /// </summary>
    public string? Trig { get; set; }

    /// <summary>
    ///     Gets or sets the prefix requirement type for the trigger.
    /// </summary>
    public RequirePrefixType Pt { get; set; }

    /// <summary>
    ///     Gets or sets the custom prefix for the trigger.
    /// </summary>
    public string? Cp { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger is disabled.
    /// </summary>
    public bool Dis { get; set; }

    /// <summary>
    ///     Gets or sets the trigger's additional responses.
    /// </summary>
    public List<string> ExtraRes { get; set; } = [];

    /// <summary>
    ///     Gets or sets how the trigger picks between its responses.
    /// </summary>
    public CtResponseMode Rm { get; set; }

    /// <summary>
    ///     Gets or sets how much currency the trigger costs to fire.
    /// </summary>
    public long Cost { get; set; }

    /// <summary>
    ///     Gets or sets how much currency the trigger pays out.
    /// </summary>
    public long Rew { get; set; }

    /// <summary>
    ///     Gets or sets how much XP the trigger grants.
    /// </summary>
    public int XpRew { get; set; }

    /// <summary>
    ///     Gets or sets the XP level required to fire the trigger.
    /// </summary>
    public int ReqLvl { get; set; }

    /// <summary>
    ///     Gets or sets the message shown when the trigger's requirements are not met.
    /// </summary>
    public string? ReqMsg { get; set; }

    /// <summary>
    ///     Gets or sets the trigger's serialized time conditions.
    /// </summary>
    public string? Times { get; set; }

    /// <summary>
    ///     Gets or sets how many times the trigger may fire.
    /// </summary>
    public int? MaxUses { get; set; }

    /// <summary>
    ///     Gets or sets the minimum account age, in minutes, required to fire the trigger.
    /// </summary>
    public int MinAge { get; set; }

    /// <summary>
    ///     Gets or sets the minimum server membership, in minutes, required to fire the trigger.
    /// </summary>
    public int MinMember { get; set; }

    /// <summary>
    ///     Gets or sets the bot event the trigger listens for.
    /// </summary>
    public CtEventType Evt { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger responds to bot and webhook messages.
    /// </summary>
    public bool Bots { get; set; }

    /// <summary>
    ///     Gets or sets the id of the trigger this one chains to.
    /// </summary>
    /// <remarks>
    ///     Trigger ids are assigned per install, so a chain only survives an import back into the same bot. It is
    ///     exported so that a backup and restore keeps working.
    /// </remarks>
    public int? Next { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the response replies to the message that fired the trigger.
    /// </summary>
    public bool Reply { get; set; }

    /// <summary>
    ///     Gets or sets how many seconds the response stays before being deleted, or 0 to keep it.
    /// </summary>
    public int DelAfter { get; set; }

    /// <summary>
    ///     Gets or sets the trigger's own cooldown, in seconds.
    /// </summary>
    public int Cd { get; set; }

    /// <summary>
    ///     Gets or sets who the trigger's cooldown applies to.
    /// </summary>
    public CtCooldownScope CdScope { get; set; }

    /// <summary>
    ///     Gets or sets the counter that gates the trigger.
    /// </summary>
    public string? Counter { get; set; }

    /// <summary>
    ///     Gets or sets the lowest counter value that allows the trigger to fire.
    /// </summary>
    public long? CounterMin { get; set; }

    /// <summary>
    ///     Gets or sets the highest counter value that allows the trigger to fire.
    /// </summary>
    public long? CounterMax { get; set; }

    /// <summary>
    ///     Gets or sets the category the trigger belongs to.
    /// </summary>
    public string? Cat { get; set; }

    /// <summary>
    ///     Converts a <see cref="DataModel.ChatTrigger" /> object to an <see cref="ExportedTriggers" /> object.
    /// </summary>
    /// <param name="ct">The <see cref="DataModel.ChatTrigger" /> object.</param>
    /// <returns>The converted <see cref="ExportedTriggers" /> object.</returns>
    public static ExportedTriggers FromModel(ChatTrigger ct)
    {
        return new ExportedTriggers
        {
            Id = "",
            Res = ct.Response,
            Ad = ct.AutoDeleteTrigger,
            At = ct.AllowTarget,
            Ca = ct.ContainsAnywhere,
            Dm = ct.DmResponse,
            Rgx = ct.IsRegex,
            React = string.IsNullOrWhiteSpace(ct.Reactions)
                ? null
                : ct.Reactions.Split("@@@"),
            Rtt = ct.ReactToTrigger,
            Nr = ct.NoRespond,
            RRole = ct.GetRemovedRoles(),
            ARole = ct.GetGrantedRoles(),
            Rgt = (CtRoleGrantType)ct.RoleGrantType,
            VTypes = (ChatTriggerType)ct.ValidTriggerTypes,
            AcName = ct.ApplicationCommandName,
            AcDesc = ct.ApplicationCommandDescription,
            Act = (CtApplicationCommandType)ct.ApplicationCommandType,
            Eph = ct.EphemeralResponse,
            Trig = ct.Trigger,
            Pt = (RequirePrefixType)ct.PrefixType,
            Cp = ct.CustomPrefix,
            Dis = ct.IsDisabled,
            ExtraRes = string.IsNullOrWhiteSpace(ct.AdditionalResponses)
                ? []
                : ct.AdditionalResponses.Split("@@@").ToList(),
            Rm = (CtResponseMode)ct.ResponseMode,
            Cost = ct.CurrencyCost,
            Rew = ct.CurrencyReward,
            XpRew = ct.XpReward,
            ReqLvl = ct.RequiredXpLevel,
            ReqMsg = ct.RequirementFailMessage,
            Times = ct.TimeConditions,
            MaxUses = ct.MaxUses,
            MinAge = ct.MinAccountAgeMinutes,
            MinMember = ct.MinServerMembershipMinutes,
            Evt = ct.EventType == 0 ? CtEventType.None : (CtEventType)ct.EventType,
            Bots = ct.AllowBots,
            Next = ct.NextTriggerId,
            Reply = ct.ReplyToTrigger,
            DelAfter = ct.DeleteResponseAfter,
            Cd = ct.CooldownSeconds,
            CdScope = (CtCooldownScope)ct.CooldownScope,
            Counter = ct.CounterName,
            CounterMin = ct.CounterMin,
            CounterMax = ct.CounterMax,
            Cat = ct.Category
        };
    }

    /// <summary>
    ///     Converts an exported trigger back into a <see cref="DataModel.ChatTrigger" /> for a guild.
    /// </summary>
    /// <param name="guildId">The guild the trigger is being imported into.</param>
    /// <param name="trigger">
    ///     The trigger text to use when the export does not carry one, which is the case for exports written by older
    ///     versions and by NadekoBot.
    /// </param>
    /// <returns>The chat trigger to insert.</returns>
    /// <remarks>
    ///     Crossposting settings are deliberately not carried across, matching the warning written into every export.
    /// </remarks>
    public ChatTrigger ToModel(ulong guildId, string trigger)
    {
        return new ChatTrigger
        {
            GuildId = guildId,
            Trigger = (string.IsNullOrWhiteSpace(Trig) ? trigger : Trig).ToLowerInvariant(),
            Response = Res,
            AutoDeleteTrigger = Ad,
            AllowTarget = At,
            ContainsAnywhere = Ca,
            DmResponse = Dm,
            IsRegex = Rgx,
            Reactions = React is { Length: > 0 } ? string.Join("@@@", React) : null,
            ReactToTrigger = Rtt,
            NoRespond = Nr,
            GrantedRoles = ARole.Count > 0 ? string.Join("@@@", ARole) : null,
            RemovedRoles = RRole.Count > 0 ? string.Join("@@@", RRole) : null,
            RoleGrantType = (int)Rgt,
            ValidTriggerTypes = (int)(VTypes == 0 ? ChatTriggerType.Message : VTypes),
            ApplicationCommandName = AcName,
            ApplicationCommandDescription = AcDesc,
            ApplicationCommandType = (int)Act,
            EphemeralResponse = Eph,
            PrefixType = (int)Pt,
            CustomPrefix = Cp,
            IsDisabled = Dis,
            AdditionalResponses = ExtraRes.Count > 0 ? string.Join("@@@", ExtraRes) : null,
            ResponseMode = (int)Rm,
            CurrencyCost = Cost,
            CurrencyReward = Rew,
            XpReward = XpRew,
            RequiredXpLevel = ReqLvl,
            RequirementFailMessage = ReqMsg,
            TimeConditions = Times,
            MaxUses = MaxUses,
            MinAccountAgeMinutes = MinAge,
            MinServerMembershipMinutes = MinMember,
            EventType = (int)Evt,
            AllowBots = Bots,
            NextTriggerId = Next,
            ReplyToTrigger = Reply,
            DeleteResponseAfter = DelAfter,
            CooldownSeconds = Cd,
            CooldownScope = (int)CdScope,
            CounterName = Counter,
            CounterMin = CounterMin,
            CounterMax = CounterMax,
            Category = Cat,
            DateAdded = DateTime.UtcNow
        };
    }
}