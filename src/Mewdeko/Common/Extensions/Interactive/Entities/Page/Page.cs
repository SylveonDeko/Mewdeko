using System;
using Discord;

namespace Mewdeko.Common.Extensions.Interactive.Entities.Page;

/// <summary>
///     Represents a message page. A page consists of a <see cref="Text" /> and an <see cref="Embed" />.
/// </summary>
public class Page
{
    internal Page(string text = null, EmbedBuilder builder = null)
    {
        Text = text;
        var isEmpty = false;

        if (builder?.Color == null &&
            builder?.Description == null &&
            builder?.Title == null &&
            builder?.Url == null &&
            builder?.ThumbnailUrl == null &&
            builder?.ImageUrl == null &&
            (builder?.Fields == null || builder.Fields.Count == 0) &&
            builder?.Footer == null &&
            builder?.Author == null &&
            builder?.Timestamp == null)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentNullException(nameof(text));

            isEmpty = true;
        }

        Embed = isEmpty ? null : builder.Build();
    }

    /// <summary>
    ///     Gets the text of this <see cref="Page" />.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the embed of this <see cref="Page" />.
    /// </summary>
    public Embed Embed { get; }

    /// <summary>
    ///     Creates a <see cref="PageBuilder" /> with all the values of this <see cref="Page" />.
    /// </summary>
    /// <returns>A <see cref="PageBuilder" />.</returns>
    public PageBuilder ToPageBuilder()
    {
        return new PageBuilder(Text, Embed.ToEmbedBuilder());
    }

    /// <summary>
    ///     Creates a new <see cref="Page" /> from an <see cref="Discord.Embed" />.
    /// </summary>
    /// <param name="embed">The embed.</param>
    /// <returns>A <see cref="Page" />.</returns>
    public static Page FromEmbed(Embed embed)
    {
        return new Page(null, embed?.ToEmbedBuilder());
    }

    /// <summary>
    ///     Creates a new <see cref="Page" /> from an <see cref="EmbedBuilder" />.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>A <see cref="Page" />.</returns>
    public static Page FromEmbedBuilder(EmbedBuilder builder)
    {
        return new Page(null, builder);
    }
}