using System.Globalization;

namespace Mewdeko.Services;

/// <summary>
///     Interface for managing localization settings.
/// </summary>
public interface ILocalization : INService
{
    /// <summary>
    ///     Gets the default culture information.
    /// </summary>
    CultureInfo? DefaultCultureInfo { get; }

    /// <summary>
    ///     Gets the culture information for a guild.
    /// </summary>
    /// <param name="guild">The guild to retrieve culture information for.</param>
    /// <returns>The culture information associated with the guild, if available; otherwise, null.</returns>
    CultureInfo? GetCultureInfo(IGuild guild);

    /// <summary>
    ///     Gets the culture information for a guild with the specified ID.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve culture information for.</param>
    /// <returns>The culture information associated with the guild, if available; otherwise, null.</returns>
    CultureInfo? GetCultureInfo(ulong? guildId);

    /// <summary>
    ///     Removes the culture information associated with a guild.
    /// </summary>
    /// <param name="guild">The guild to remove culture information for.</param>
    void RemoveGuildCulture(IGuild guild);

    /// <summary>
    ///     Resets the default culture to the system default.
    /// </summary>
    void ResetDefaultCulture();

    /// <summary>
    ///     Sets the default culture information.
    /// </summary>
    /// <param name="ci">The culture information to set as the default.</param>
    void SetDefaultCulture(CultureInfo? ci);

    /// <summary>
    ///     Sets the culture information for a guild.
    /// </summary>
    /// <param name="guild">The guild to set culture information for.</param>
    /// <param name="ci">The culture information to associate with the guild.</param>
    void SetGuildCulture(IGuild guild, CultureInfo? ci);
}