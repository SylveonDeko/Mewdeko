using Discord;
using Discord.Interactions;
using MoreLinq;

namespace Mewdeko.Extensions;

public static class ModalBuilderExtensions
{
    public static void UpdateTextInputValue(this ModalBuilder builder, string customId, string value)
    {
        var components = builder.Components.ActionRows.SelectMany(x => x.Components).ToList();
        var comp = components.First(x => x.CustomId == customId) as TextInputComponent;

        var tib = new TextInputBuilder()
        {
            CustomId = customId,
            Label = comp.Label,
            MaxLength = comp.MaxLength,
            MinLength = comp.MinLength,
            Placeholder = comp.Placeholder,
            Style = comp.Style,
            Value = value
        };
        builder.Components.ActionRows.RemoveAll(x => x.Components.Any(x => x.CustomId == customId));
        builder.Components.ActionRows.Add(new ActionRowBuilder().AddComponent(tib.Build()));
    }
}
