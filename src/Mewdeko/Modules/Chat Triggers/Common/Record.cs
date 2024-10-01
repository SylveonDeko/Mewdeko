namespace Mewdeko.Modules.Chat_Triggers.Common;

/// <summary>
///     Represents an error that occurred during interaction with chat triggers.
/// </summary>
public record ChatTriggersInteractionError(string ErrorKey, int[] CtIds, string[] CtRealNames);