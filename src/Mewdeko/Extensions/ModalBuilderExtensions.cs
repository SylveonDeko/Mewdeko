namespace Mewdeko.Extensions;

public static class ModalBuilderExtensions
{
    public static ModalBuilder UpdateTextInput(
        this ModalBuilder builder,
        string customId,
        Action<TextInputBuilder> input)
    {
        var components = builder.Components.ActionRows.SelectMany(x => x.Components).ToList();
        var comp = components.First(x => x.CustomId == customId) as TextInputComponent;

        var tib = new TextInputBuilder
        {
            CustomId = customId,
            Label = comp.Label,
            MaxLength = comp.MaxLength,
            MinLength = comp.MinLength,
            Placeholder = comp.Placeholder,
            Style = comp.Style,
            Value = comp.Value
        };

        input(tib);

        builder.Components.ActionRows.RemoveAll(x => x.Components.Any(messageComponent => messageComponent.CustomId == customId));
        builder.Components.ActionRows.Add(new ActionRowBuilder().AddComponent(tib.Build()));

        return builder;
    }
}