using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Services.strings;

namespace Mewdeko.Common.TypeReaders.Interactions;

public class EmoteArrayTypeConverter : MewdekoTypeReader<IEmote[]>
{
    private IBotStrings Strings { get; set; }

    public EmoteArrayTypeConverter(DiscordSocketClient client, InteractionService cmds, IBotStrings strings) : base(client, cmds) => Strings = strings;

    public override bool CanConvertTo(Type type) => type.IsArray && type.GetElementType() == typeof(IEmote);

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, string option, IServiceProvider services)
    {
        var emotes = Regex.Split(option, "[.|, +]+")
            .Select(x => x.TryToIEmote(out var value) ? value : null)
            .Where(x => x is not null);
        return Task.FromResult(!emotes.Any()
            ? TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, Strings.GetText("emote_reader_none_found", context.Guild?.Id))
            : TypeConverterResult.FromSuccess(emotes.ToArray()));
    }
}