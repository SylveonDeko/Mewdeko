namespace Mewdeko.Modules.Switch.Common;

/// <summary>
///     The result of resolving a Nintendo Switch error code.
/// </summary>
/// <param name="ErrorCode">The error code formatted as <c>NNNN-NNNN</c>.</param>
/// <param name="HexCode">The error code formatted as hexadecimal.</param>
/// <param name="ModuleId">The numeric module id.</param>
/// <param name="ModuleName">The human-readable module name, or <c>null</c> if the module id isn't recognized.</param>
/// <param name="Description">The numeric description id.</param>
/// <param name="ErrorDescription">The human-readable description of the error, or empty if not known.</param>
/// <param name="IsKnownDescription">Whether a specific description (as opposed to the fallback text) was found.</param>
public sealed record SwitchErrorLookup(
    string ErrorCode,
    string HexCode,
    int ModuleId,
    string? ModuleName,
    int Description,
    string ErrorDescription,
    bool IsKnownDescription);

/// <summary>
///     The result of resolving a Switch error code that doesn't follow the standard NNNN-NNNN format.
/// </summary>
/// <param name="GameName">The name of the game the error belongs to.</param>
/// <param name="ErrorDescription">The human-readable description of the error.</param>
public sealed record SwitchGameErrorLookup(string GameName, string ErrorDescription);