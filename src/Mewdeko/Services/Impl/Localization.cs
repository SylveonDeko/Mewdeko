using System.Globalization;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Serilog;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Provides functionality for managing localization settings and retrieving localized data.
/// </summary>
/// <param name="bss">The bss service.</param>
/// <param name="service">The service service.</param>
/// <param name="stringsProvider">Used to check which locales we actually have string data for.</param>
public class Localization(BotConfigService bss, GuildSettingsService service, IBotStringsProvider stringsProvider)
    : ILocalization
{
    /// <inheritdoc />
    public CultureInfo? DefaultCultureInfo
    {
        get
        {
            var configured = bss.Data.DefaultLocale;
            if (configured is null || IsSupported(configured.Name))
                return configured;

            // The persisted default locale isn't one we have strings for (e.g. a neutral culture
            // like "en" instead of "en-US"). Self-heal it so we don't repeat this lookup forever.
            if (!TryResolveCulture(configured.Name, out var resolved))
                return configured;

            Log.Warning(
                "Bot default locale '{Old}' isn't supported, correcting to '{New}'",
                configured.Name, resolved!.Name);
            bss.ModifyConfig(bs => bs.DefaultLocale = resolved);
            return resolved;
        }
    }

    /// <inheritdoc />
    public void SetGuildCulture(IGuild guild, CultureInfo? ci)
    {
        SetGuildCulture(guild.Id, ci);
    }

    /// <inheritdoc />
    public void RemoveGuildCulture(IGuild guild)
    {
        RemoveGuildCulture(guild.Id);
    }

    /// <inheritdoc />
    public void SetDefaultCulture(CultureInfo? ci)
    {
        bss.ModifyConfig(bs => bs.DefaultLocale = ci);
    }

    /// <inheritdoc />
    public void ResetDefaultCulture()
    {
        SetDefaultCulture(CultureInfo.CurrentCulture);
    }

    /// <inheritdoc />
    public CultureInfo? GetCultureInfo(IGuild? guild)
    {
        return GetCultureInfo(guild?.Id);
    }

    /// <inheritdoc />
    public CultureInfo? GetCultureInfo(ulong? guildId)
    {
        if (!guildId.HasValue)
            return DefaultCultureInfo;

        var guildConfig = service.GetGuildConfig(guildId.Value).ConfigureAwait(false).GetAwaiter().GetResult();

        if (guildConfig.Locale == null)
            return DefaultCultureInfo;

        if (TryResolveCulture(guildConfig.Locale, out var resolved))
        {
            if (resolved!.Name != guildConfig.Locale)
            {
                // Self-heal: the stored locale (e.g. "en") isn't one we have strings for as-is,
                // but it resolved to a supported one (e.g. "en-US"). Persist the fix.
                Log.Warning(
                    "Guild {GuildId} locale '{Old}' isn't supported, correcting to '{New}'",
                    guildId.Value, guildConfig.Locale, resolved.Name);
                guildConfig.Locale = resolved.Name;
                service.UpdateGuildConfig(guildId.Value, guildConfig).ConfigureAwait(false).GetAwaiter().GetResult();
            }

            return resolved;
        }

        // Completely unsupported locale (e.g. bad data, or a locale that's since been removed) -
        // fall back to default rather than repeatedly failing every string lookup for this guild.
        Log.Warning(
            "Guild {GuildId} locale '{Old}' isn't supported and has no fallback, resetting to default",
            guildId.Value, guildConfig.Locale);
        guildConfig.Locale = null;
        service.UpdateGuildConfig(guildId.Value, guildConfig).ConfigureAwait(false).GetAwaiter().GetResult();
        return DefaultCultureInfo;
    }

    /// <inheritdoc />
    public bool TryResolveCulture(string? name, out CultureInfo? resolved)
    {
        resolved = null;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        CultureInfo culture;
        try
        {
            culture = new CultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }

        if (IsSupported(culture.Name))
        {
            resolved = culture;
            return true;
        }

        if (!culture.IsNeutralCulture)
            return false;

        CultureInfo specific;
        try
        {
            specific = CultureInfo.CreateSpecificCulture(culture.Name);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }

        if (!IsSupported(specific.Name))
            return false;

        resolved = specific;
        return true;
    }

    private bool IsSupported(string localeName)
    {
        return stringsProvider.GetAvailableLocales().Contains(localeName);
    }

    /// <summary>
    ///     Sets the culture info for the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="ci">The culture info to set.</param>
    private async void SetGuildCulture(ulong guildId, CultureInfo? ci)
    {
        if (ci?.Name == bss.Data.DefaultLocale?.Name)
        {
            RemoveGuildCulture(guildId);
            return;
        }


        var gc = await service.GetGuildConfig(guildId);
        gc.Locale = ci.Name;
        await service.UpdateGuildConfig(guildId, gc);
    }

    /// <summary>
    ///     Removes the culture info for the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    private async void RemoveGuildCulture(ulong guildId)
    {
        var gc = await service.GetGuildConfig(guildId);
        gc.Locale = null;
        await service.UpdateGuildConfig(guildId, gc);
    }
}