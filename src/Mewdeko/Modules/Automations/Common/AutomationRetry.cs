using System.Net;
using System.Net.Http;
using Discord.Net;

namespace Mewdeko.Modules.Automations.Common;

/// <summary>
///     Decides which failures are worth trying again.
/// </summary>
public static class AutomationRetry
{
    /// <summary>
    ///     Checks whether a failure is a rate limit or a temporary Discord problem. Permission errors and anything
    ///     caused by the automation's own settings are never worth trying again.
    /// </summary>
    /// <param name="exception">The failure.</param>
    public static bool IsTransient(Exception exception) => exception switch
    {
        AutomationActionException => false,
        RateLimitedException => true,
        HttpException http => IsTransient(http.HttpCode, http.DiscordCode),
        TimeoutException => true,
        TaskCanceledException => true,
        HttpRequestException => true,
        _ => false
    };

    /// <summary>
    ///     Checks whether an HTTP status is a rate limit or a temporary Discord problem.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="discordCode">The Discord error code, if one came back.</param>
    public static bool IsTransient(HttpStatusCode status, DiscordErrorCode? discordCode)
    {
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            return false;

        if (discordCode is DiscordErrorCode.InsufficientPermissions or DiscordErrorCode.MissingPermissions
            or DiscordErrorCode.CannotSendMessageToUser)
            return false;

        return status == HttpStatusCode.TooManyRequests || (int)status >= 500;
    }

    /// <summary>
    ///     Gets how long to wait before the next try.
    /// </summary>
    /// <param name="attempt">The try that just failed, starting at 1.</param>
    public static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(Math.Min(8, Math.Pow(2, attempt - 1)));

    /// <summary>
    ///     Writes a failure as a short reason for the run log.
    /// </summary>
    /// <param name="exception">The failure.</param>
    public static string Describe(Exception exception) => exception switch
    {
        AutomationActionException action => action.Message,
        RateLimitedException => "Rate limited by Discord.",
        HttpException { DiscordCode: DiscordErrorCode.CannotSendMessageToUser } =>
            "The member has direct messages closed.",
        HttpException { HttpCode: HttpStatusCode.Forbidden } => "Discord says the bot is missing permissions.",
        HttpException { HttpCode: HttpStatusCode.NotFound } => "The target no longer exists.",
        HttpException http => $"Discord error {(int)http.HttpCode}{(http.Reason is { Length: > 0 } ? $": {http.Reason}" : "")}.",
        TimeoutException or TaskCanceledException => "Discord took too long to answer.",
        _ => "Something went wrong."
    };
}

/// <summary>
///     A failure of an action that is caused by its settings or the bot's permissions, with a reason fit for the run
///     log.
/// </summary>
public sealed class AutomationActionException(string reason) : Exception(reason);
