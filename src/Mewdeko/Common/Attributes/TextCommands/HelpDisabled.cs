namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
///     Attribute to disable help for a command or method.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class HelpDisabled : Attribute;