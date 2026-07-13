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
    public CultureInfo? DefaultCultureInfo { get; }

    /// <summary>
    ///     Gets the culture information for a guild.
    /// </summary>
    /// <param name="guild">The guild to retrieve culture information for.</param>
    /// <returns>The culture information associated with the guild, if available; otherwise, null.</returns>
    public CultureInfo? GetCultureInfo(IGuild guild);

    /// <summary>
    ///     Gets the culture information for a guild with the specified ID.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve culture information for.</param>
    /// <returns>The culture information associated with the guild, if available; otherwise, null.</returns>
    public CultureInfo? GetCultureInfo(ulong? guildId);

    /// <summary>
    ///     Removes the culture information associated with a guild.
    /// </summary>
    /// <param name="guild">The guild to remove culture information for.</param>
    public void RemoveGuildCulture(IGuild guild);

    /// <summary>
    ///     Resets the default culture to the system default.
    /// </summary>
    public void ResetDefaultCulture();

    /// <summary>
    ///     Sets the default culture information.
    /// </summary>
    /// <param name="ci">The culture information to set as the default.</param>
    public void SetDefaultCulture(CultureInfo? ci);

    /// <summary>
    ///     Sets the culture information for a guild.
    /// </summary>
    /// <param name="guild">The guild to set culture information for.</param>
    /// <param name="ci">The culture information to associate with the guild.</param>
    public void SetGuildCulture(IGuild guild, CultureInfo? ci);

    /// <summary>
    ///     Attempts to resolve a locale name (e.g. "en-US", or a neutral culture like "en") to a culture we
    ///     actually have string data for. Neutral cultures are mapped to their default specific culture
    ///     (e.g. "en" -&gt; "en-US") when that specific culture is supported.
    /// </summary>
    /// <param name="name">The locale name to resolve.</param>
    /// <param name="resolved">The resolved, supported culture, if resolution succeeded.</param>
    /// <returns>True if <paramref name="name" /> resolved to a supported culture.</returns>
    public bool TryResolveCulture(string? name, out CultureInfo? resolved);
}