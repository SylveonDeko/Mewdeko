namespace Mewdeko.Modules.Chat_Triggers.Common;

public record ChatTriggersInteractionError(string ErrorKey, int[] CtIds, string[] CtRealNames);