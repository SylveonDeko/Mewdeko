using System.Diagnostics;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Represents a unit of measurement for conversion processes,
///     including triggers for identification and a modifier for the conversion.
/// </summary>
[DebuggerDisplay("Type: {UnitType} Trigger: {Triggers[0]} Mod: {Modifier}")]
public class ConvertUnit
{
    /// <summary>
    ///     Gets or sets the triggers that identify the unit for conversion.
    /// </summary>
    public string[] Triggers { get; set; }

    /// <summary>
    ///     Gets or sets the type of the unit, e.g., "Length", "Weight".
    /// </summary>
    public string UnitType { get; set; }

    /// <summary>
    ///     Gets or sets the modifier used in conversion calculations for this unit.
    /// </summary>
    public decimal Modifier { get; set; }
}