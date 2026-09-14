using System.Text.RegularExpressions;
using Discord.Interactions;

namespace Mewdeko.Common.TypeReaders.Interactions;

/// <summary>
///     Converts a space separated string of channel mentions, ids, or names into an array of guild channels.
/// </summary>
public partial class ChannelArrayConverter : TypeConverter<IGuildChannel[]>
{
    /// <summary>
    ///     Returns the Discord type of the option.
    /// </summary>
    public override ApplicationCommandOptionType GetDiscordType()
    {
        return ApplicationCommandOptionType.String;
    }

    /// <summary>
    ///     Converts the given string to an array of guild channels.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="input">The option to convert.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The conversion result.</returns>
    public override async Task<TypeConverterResult> ReadAsync(IInteractionContext context,
        IApplicationCommandInteractionDataOption input, IServiceProvider services)
    {
        var option = input.Value as string ?? "";
        var parts = option.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var channels = await context.Guild.GetChannelsAsync();
        var result = new List<IGuildChannel>();
        var regex = ChannelRegex();

        foreach (var part in parts)
        {
            IGuildChannel? channel;
            if (regex.Match(part) is { Success: true } match && ulong.TryParse(match.Groups[1].Value, out var id) ||
                ulong.TryParse(part, out id))
                channel = channels.FirstOrDefault(c => c.Id == id);
            else
                channel = channels.FirstOrDefault(c =>
                    string.Equals(c.Name, part, StringComparison.OrdinalIgnoreCase));

            if (channel is null)
                return TypeConverterResult.FromError(InteractionCommandError.ParseFailed,
                    $"Channel {part} not found.");

            result.Add(channel);
        }

        return TypeConverterResult.FromSuccess(result.ToArray());
    }

    /// <summary>
    ///     Writes the properties of the option.
    /// </summary>
    /// <param name="properties">The properties of the option.</param>
    /// <param name="parameter">The parameter information.</param>
    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        properties.Description = "Mention, name, or id. Separate channels with a space.";
    }

    [GeneratedRegex("<#([0-9]+)>")]
    private static partial Regex ChannelRegex();
}