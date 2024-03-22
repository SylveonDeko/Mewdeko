using System.Text.RegularExpressions;
using Discord.Interactions;
using Mewdeko.Services.strings;

namespace Mewdeko.Common.TypeReaders.Interactions;

/// <summary>
/// Class that converts a string to an array of IEmote objects.
/// </summary>
public class EmoteArrayTypeConverter : MewdekoTypeReader<IEmote[]>
{
    /// <summary>
    /// The bot strings service.
    /// </summary>
    private IBotStrings Strings { get; set; }

    /// <summary>
    /// Initializes a new instance of the EmoteArrayTypeConverter class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="cmds">The interaction service.</param>
    /// <param name="strings">The bot strings service.</param>
    public EmoteArrayTypeConverter(DiscordSocketClient client, InteractionService cmds, IBotStrings strings) :
        base(client, cmds) => Strings = strings;

    /// <summary>
    /// Checks if the given type can be converted to an array of IEmote objects.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type can be converted to an array of IEmote objects, false otherwise.</returns>
    public override bool CanConvertTo(Type type) => type.IsArray && type.GetElementType() == typeof(IEmote);

    /// <summary>
    /// Converts the given string to an array of IEmote objects.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="option">The string to convert.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the conversion result.</returns>
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, string option,
        IServiceProvider services)
    {
        var emotes = Regex.Split(option, "[.|, +]+")
            .Select(x => x.TryToIEmote(out var value) ? value : null)
            .Where(x => x is not null);
        return Task.FromResult(!emotes.Any()
            ? TypeConverterResult.FromError(InteractionCommandError.ConvertFailed,
                Strings.GetText("emote_reader_none_found", context.Guild?.Id))
            : TypeConverterResult.FromSuccess(emotes.ToArray()));
    }
}