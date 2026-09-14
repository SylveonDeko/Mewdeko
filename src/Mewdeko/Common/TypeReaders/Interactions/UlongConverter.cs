using Discord.Interactions;

namespace Mewdeko.Common.TypeReaders.Interactions;

/// <summary>
///     Converts a string option into a ulong. Discord integer options are JavaScript numbers and lose precision
///     above 2^53, so snowflake ids must be transported as strings.
/// </summary>
public class UlongConverter : TypeConverter<ulong>
{
    /// <summary>
    ///     Returns the Discord type of the option.
    /// </summary>
    public override ApplicationCommandOptionType GetDiscordType()
    {
        return ApplicationCommandOptionType.String;
    }

    /// <summary>
    ///     Parses the given string option into a ulong.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="input">The option to convert.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The conversion result.</returns>
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context,
        IApplicationCommandInteractionDataOption input, IServiceProvider services)
    {
        var raw = input.Value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ParseFailed,
                "A numeric id is required."));

        var digits = raw.Trim('<', '>', '@', '#', '&', '!');
        return ulong.TryParse(digits, out var value)
            ? Task.FromResult(TypeConverterResult.FromSuccess(value))
            : Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ParseFailed,
                $"{raw} is not a valid id."));
    }

    /// <summary>
    ///     Writes the properties of the option.
    /// </summary>
    /// <param name="properties">The properties of the option.</param>
    /// <param name="parameter">The parameter information.</param>
    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        properties.MinLength ??= 1;
        properties.MaxLength ??= 22;
    }
}