using Discord.Interactions;
using Mewdeko.Services.strings;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mewdeko.Common.TypeReaders.Interactions;

public class IEmoteArrayTypeConverter<T> : MewdekoTypeReader<IEmote[]>
{
    private IBotStrings _strings { get; set; }

    public IEmoteArrayTypeConverter(DiscordSocketClient client, InteractionService cmds, IBotStrings strings) : base(client, cmds) => _strings = strings;

    public override bool CanConvertTo(Type type) => type.IsArray && type.GetElementType() == typeof(IEmote);

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, string option, IServiceProvider services)
    {
        var emotes = Regex.Split(option, "[.|, +]+")
            .Select(x => x.TryToIEmote(out var value) ? value : null)
            .Where(x => x is not null);
        if (!emotes.Any())
            return Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, _strings.GetText("emote_reader_none_found", context.Guild?.Id)));
        return Task.FromResult(TypeConverterResult.FromSuccess(emotes.ToArray()));
    }
}