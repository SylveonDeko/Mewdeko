using System.Globalization;
using Mewdeko.Modules.Twitch.Services;
using Mewdeko.Services.strings;

// ReSharper disable NotNullOrRequiredMemberIsNotInitialized

namespace Mewdeko.Modules.Twitch.Common;

/// <summary>
///     Base class for all Twitch chat command modules. Mirrors the role of
///     <c>MewdekoSlashCommandModule</c> for Discord: the context, strings service, and send helpers
///     are all properties rather than method parameters.
/// </summary>
public abstract class TwitchModuleBase
{
    /// <summary>
    ///     The bot strings service. Used to fetch localized response text via <see cref="GetText" />.
    ///     Injected by the DI container.
    /// </summary>
    public IBotStrings Strings { get; set; }

    /// <summary>
    ///     The Twitch service used to send replies back to IRC chat.
    ///     Injected by the DI container.
    /// </summary>
    public TwitchService TwitchSvc { get; set; }

    /// <summary>
    ///     Gets or sets the command context for the current invocation.
    ///     Set by <c>TwitchCommandHandler</c> immediately before the command method is called.
    /// </summary>
    public TwitchCommandContext Context { get; set; }

    /// <summary>
    ///     Gets a localized string by key, honouring the Twitch channel's own language if configured
    ///     via <c>TwitchGuildConfig.Language</c>. Falls back to the guild locale, then the bot default.
    /// </summary>
    /// <param name="key">The snake_case localization key from the responses JSON files.</param>
    /// <param name="data">Optional format arguments.</param>
    protected string GetText(string key, params object[] data)
    {
        var lang = Context.ChannelLanguage;
        if (!string.IsNullOrWhiteSpace(lang))
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(lang);
                return Strings.GetText(key, culture, data);
            }
            catch
            {
                // invalid culture tag, fall through to guild locale
            }
        }

        return Strings.GetText(key, Context.GuildId, data);
    }

    /// <summary>
    ///     Sends a localized reply to the Twitch channel, prefixed with <c>@DisplayName</c>.
    /// </summary>
    /// <param name="key">The snake_case localization key.</param>
    /// <param name="data">Optional format arguments.</param>
    protected Task ReplyLocalizedAsync(string key, params object[] data)
    {
        return TwitchSvc.SendMessageAsync(Context.TwitchChannel, $"@{Context.DisplayName} {GetText(key, data)}");
    }

    /// <summary>
    ///     Sends a plain reply to the Twitch channel, prefixed with <c>@DisplayName</c>.
    ///     Prefer <see cref="ReplyLocalizedAsync" /> for any user-visible text.
    /// </summary>
    /// <param name="message">The already-resolved message to send.</param>
    protected Task ReplyAsync(string message)
    {
        return TwitchSvc.SendMessageAsync(Context.TwitchChannel, $"@{Context.DisplayName} {message}");
    }

    /// <summary>
    ///     Sends a plain message to the Twitch channel without a user mention prefix.
    /// </summary>
    /// <param name="message">The already-resolved message to send.</param>
    protected Task SayAsync(string message)
    {
        return TwitchSvc.SendMessageAsync(Context.TwitchChannel, message);
    }
}

/// <summary>
///     Base class for Twitch command modules that also inject a typed service, analogous to
///     <c>MewdekoSlashModuleBase&lt;TService&gt;</c>.
/// </summary>
/// <typeparam name="TService">The service type to inject alongside the Twitch infrastructure.</typeparam>
public abstract class TwitchModuleBase<TService> : TwitchModuleBase
{
    /// <summary>
    ///     The service associated with this module. Injected by the DI container.
    /// </summary>
    public TService Service { get; set; }
}