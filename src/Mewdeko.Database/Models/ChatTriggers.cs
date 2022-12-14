namespace Mewdeko.Database.Models;

public class ChatTriggers : DbEntity
{
    public ulong UseCount { get; set; }
    public bool IsRegex { get; set; }
    public bool OwnerOnly { get; set; }

    public ulong? GuildId { get; set; }
    public string Response { get; set; }
    public string Trigger { get; set; }

    public RequirePrefixType PrefixType { get; set; } = RequirePrefixType.None;
    public string CustomPrefix { get; set; } = "";

    public bool AutoDeleteTrigger { get; set; }
    public bool ReactToTrigger { get; set; }
    public bool NoRespond { get; set; }
    public bool DmResponse { get; set; }
    public bool ContainsAnywhere { get; set; }
    public bool AllowTarget { get; set; }
    public string Reactions { get; set; }

    public string GrantedRoles { get; set; } = "";
    public string RemovedRoles { get; set; } = "";
    public CtRoleGrantType RoleGrantType { get; set; }

    public string[] GetReactions() =>
        string.IsNullOrWhiteSpace(Reactions)
            ? Array.Empty<string>()
            : Reactions.Split("@@@");

    public bool IsGlobal() => GuildId is null or 0;

    public ChatTriggerType ValidTriggerTypes { get; set; } = (ChatTriggerType)0b1111;
    public ulong ApplicationCommandId { get; set; } = 0;
    public string ApplicationCommandName { get; set; } = "";
    public string ApplicationCommandDescription { get; set; } = "";
    public CtApplicationCommandType ApplicationCommandType { get; set; } = CtApplicationCommandType.None;
    public bool EphemeralResponse { get; set; } = false;
    public ulong CrosspostingChannelId { get; set; } = 0;
    public string CrosspostingWebhookUrl { get; set; } = "";

    public string RealName => (string.IsNullOrEmpty(ApplicationCommandName) ? Trigger : ApplicationCommandName).Trim();
}

public class ReactionResponse : DbEntity
{
    public bool OwnerOnly { get; set; }
    public string Text { get; set; }
}

public enum CtRoleGrantType
{
    Sender,
    Mentioned,
    Both
}

public enum CtApplicationCommandType
{
    None,
    Slash,
    Message,
    User
}

[Flags]
public enum ChatTriggerType
{
    Message = 0b0001,
    Interaction = 0b0010,

    Button = 0b0100
    // not yet developed
    // Reactions = 0b10000,
}

public enum RequirePrefixType
{
    None,
    Global,
    GuildOrGlobal,
    GuildOrNone,
    Custom
}