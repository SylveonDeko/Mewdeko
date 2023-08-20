namespace Mewdeko.Database.Models;

public class ChatTriggers : DbEntity
{
    public ulong UseCount { get; set; }
    public long IsRegex { get; set; }
    public long OwnerOnly { get; set; }

    public ulong? GuildId { get; set; }
    public string Response { get; set; }
    public string Trigger { get; set; }

    public RequirePrefixType PrefixType { get; set; } = RequirePrefixType.None;
    public string CustomPrefix { get; set; } = "";

    public long AutoDeleteTrigger { get; set; }
    public long ReactToTrigger { get; set; }
    public long NoRespond { get; set; }
    public long DmResponse { get; set; }
    public long ContainsAnywhere { get; set; }
    public long AllowTarget { get; set; }
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
    public long EphemeralResponse { get; set; } = 0;
    public ulong CrosspostingChannelId { get; set; } = 0;
    public string CrosspostingWebhookUrl { get; set; } = "";

    public string RealName => (string.IsNullOrEmpty(ApplicationCommandName) ? Trigger : ApplicationCommandName).Trim();
}

public class ReactionResponse : DbEntity
{
    public long OwnerOnly { get; set; }
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