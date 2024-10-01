using Discord.Commands;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.TypeReaders;

/// <summary>
///     Type reader for parsing guild-specific DateTime inputs into GuildDateTime objects.
/// </summary>
public class GuildDateTimeTypeReader : MewdekoTypeReader<GuildDateTime>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GuildDateTimeTypeReader" /> class.
    /// </summary>
    /// <param name="client">The discord client</param>
    /// <param name="cmds">The command service</param>
    public GuildDateTimeTypeReader(DiscordShardedClient client, CommandService cmds) : base(client, cmds)
    {
    }

    /// <inheritdoc />
    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
        IServiceProvider services)
    {
        var gdt = Parse(services, context.Guild.Id, input);
        if (gdt == null)
        {
            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed,
                "Input string is in an incorrect format."));
        }

        return Task.FromResult(TypeReaderResult.FromSuccess(gdt));
    }

    /// <summary>
    ///     Parses the input string into a GuildDateTime object.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="input">The input string representing the DateTime.</param>
    /// <returns>A GuildDateTime object if parsing is successful; otherwise, null.</returns>
    public static GuildDateTime? Parse(IServiceProvider services, ulong guildId, string input)
    {
        var gts = services
            .GetService<GuildTimezoneService>(); // Retrieves the GuildTimezoneService instance from services

        if (!DateTime.TryParse(input, out var dt)) // Attempts to parse the input string into a DateTime
            return null;

        var tz = gts?.GetTimeZoneOrUtc(guildId); // Retrieves the guild's timezone

        return new GuildDateTime(tz, dt); // Constructs and returns a GuildDateTime object
    }
}

/// <summary>
///     Represents a DateTime with guild-specific timezone information.
/// </summary>
public class GuildDateTime
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GuildDateTime" /> class.
    /// </summary>
    /// <param name="guildTimezone">The timezone of the guild.</param>
    /// <param name="inputTime">The input DateTime.</param>
    public GuildDateTime(TimeZoneInfo guildTimezone, DateTime inputTime)
    {
        var now = DateTime.UtcNow; // Gets the current UTC time
        Timezone = guildTimezone; // Sets the timezone of the guild
        CurrentGuildTime =
            TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Utc,
                Timezone); // Converts the current UTC time to the guild's local time
        InputTime = inputTime; // Sets the input time
        InputTimeUtc =
            TimeZoneInfo.ConvertTime(inputTime, Timezone, TimeZoneInfo.Utc); // Converts the input time to UTC
    }

    /// <summary>
    ///     Gets the timezone of the guild.
    /// </summary>
    public TimeZoneInfo Timezone { get; }

    /// <summary>
    ///     Gets the current time in the guild's timezone.
    /// </summary>
    public DateTime CurrentGuildTime { get; }

    /// <summary>
    ///     Gets the input time.
    /// </summary>
    public DateTime InputTime { get; }

    /// <summary>
    ///     Gets the input time in UTC.
    /// </summary>
    public DateTime InputTimeUtc { get; }
}