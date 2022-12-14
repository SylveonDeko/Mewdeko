using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.TypeReaders;

public class GuildDateTimeTypeReader : MewdekoTypeReader<GuildDateTime>
{
    public GuildDateTimeTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
    {
    }

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

    public static GuildDateTime? Parse(IServiceProvider services, ulong guildId, string input)
    {
        var gts = services.GetService<GuildTimezoneService>();
        if (!DateTime.TryParse(input, out var dt))
            return null;

        var tz = gts?.GetTimeZoneOrUtc(guildId);

        return new GuildDateTime(tz, dt);
    }
}

public class GuildDateTime
{
    public GuildDateTime(TimeZoneInfo guildTimezone, DateTime inputTime)
    {
        var now = DateTime.UtcNow;
        Timezone = guildTimezone;
        CurrentGuildTime = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Utc, Timezone);
        InputTime = inputTime;
        InputTimeUtc = TimeZoneInfo.ConvertTime(inputTime, Timezone, TimeZoneInfo.Utc);
    }

    public TimeZoneInfo Timezone { get; }
    public DateTime CurrentGuildTime { get; }
    public DateTime InputTime { get; }
    public DateTime InputTimeUtc { get; }
}