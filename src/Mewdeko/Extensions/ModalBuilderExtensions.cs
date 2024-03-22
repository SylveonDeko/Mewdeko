namespace Mewdeko.Extensions;

/// <summary>
/// Contains extension methods for <see cref="ModalBuilder"/>.
/// </summary>
public static class ModalBuilderExtensions
{
    /// <summary>
    /// Updates a text input component in a modal builder.
    /// </summary>
    /// <param name="builder">The modal builder to update.</param>
    /// <param name="customId">The custom ID of the text input component to update.</param>
    /// <param name="input">The action to update the text input component.</param>
    /// <returns>The updated modal builder.</returns>
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

        builder.Components.ActionRows.RemoveAll(x =>
            x.Components.Any(messageComponent => messageComponent.CustomId == customId));
        builder.Components.ActionRows.Add(new ActionRowBuilder().AddComponent(tib.Build()));

        return builder;
    }
}