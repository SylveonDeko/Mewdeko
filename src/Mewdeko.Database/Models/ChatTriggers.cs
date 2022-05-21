using LinqToDB.Common;

namespace Mewdeko.Database.Models;

public class ChatTriggers : DbEntity
{
    public ulong UseCount { get; set; }
    public bool IsRegex { get; set; }
    public bool OwnerOnly { get; set; }

    public ulong? GuildId { get; set; }
    public string Response { get; set; }
    public string Trigger { get; set; }

    public bool AutoDeleteTrigger { get; set; }
    public bool ReactToTrigger { get; set; }
    public bool NoRespond { get; set; }
    public bool DmResponse { get; set; }
    public bool ContainsAnywhere { get; set; }
    public bool AllowTarget { get; set; }
    public string Reactions { get; set; }

    public string GrantedRoles { get; set; }
    public string RemovedRoles { get; set; }
    public CTRoleGrantType RoleGrantType { get; set; }

    public string[] GetReactions() =>
        string.IsNullOrWhiteSpace(Reactions)
            ? Array.Empty<string>()
            : Reactions.Split("@@@");

    public bool IsGlobal() => GuildId is null or 0;

    public ulong ApplicationCommandId { get; set; } = 0;
    public string ApplicationCommandName { get; set; } = "";
    public string ApplicationCommandDescription { get; set; } = "";
    public CTApplicationCommandType ApplicationCommandType { get; set; } = CTApplicationCommandType.None;
    public bool EphemeralResponse { get; set; } = false;

    public string RealName => ApplicationCommandName.IsNullOrWhiteSpace() ? Trigger : ApplicationCommandName;
}

public class ReactionResponse : DbEntity
{
    public bool OwnerOnly { get; set; }
    public string Text { get; set; }
}

public enum CTRoleGrantType
{
    Sender,
    Mentioned,
    Both
}

public enum CTApplicationCommandType
{
    None,
    Slash,
    Message,
    User
}