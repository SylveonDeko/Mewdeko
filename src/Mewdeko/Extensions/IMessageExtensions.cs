using Serilog;
using Embed = Mewdeko.Common.Embed;
using Image = Mewdeko.Common.Image;

namespace Mewdeko.Extensions;

public static class MessageExtensions
{
    public static string GetJumpLink(this IMessage message)
        => $"https://discord.com/channels/{(message.Channel is ITextChannel channel ? channel.GuildId : "@me")}/{message.Channel.Id}/{message.Id}";

    public static void ReplyError(this IUserMessage message, string content)
    {
        var eb = new EmbedBuilder().WithErrorColor().WithDescription(content);
        var builder = new ComponentBuilder().WithButton("Support Server", style: ButtonStyle.Link, url: "discord.gg/mewdeko");
        message.ReplyAsync(embed: eb.Build(), components: builder.Build());
    }

    public static NewEmbed GetNewEmbedSource(this IMessage message)
    {
        try
        {
            var eb = new NewEmbed
            {
                Embeds = new List<Embed>(), Components = new List<NewEmbed.NewEmbedComponent>()
            };
            var embedList = new List<Embed>();
            var componentList = new List<NewEmbed.NewEmbedComponent>();
            eb.Content = message.Content;
            foreach (var i in message.Embeds)
            {
                if (i.Type is not EmbedType.Rich)
                    continue;
                var e = new Embed
                {
                    Fields = new List<Field>()
                };
                if (i.Title != null)
                    e.Title = i.Title;
                if (i.Description != null)
                    e.Description = i.Description;
                if (i.Url != null)
                    e.Url = i.Url;
                if (i.Author.HasValue)
                {
                    e.Author = new Author
                    {
                        Name = i.Author.Value.Name, Url = i.Author.Value.Url, IconUrl = i.Author.Value.IconUrl
                    };
                }

                if (i.Fields.Any())
                {
                    foreach (var field in i.Fields)
                    {
                        e.Fields.Add(new Field
                        {
                            Name = field.Name, Value = field.Value, Inline = field.Inline
                        });
                    }
                }

                if (i.Footer.HasValue)
                {
                    e.Footer = new Footer
                    {
                        Text = i.Footer.Value.Text, IconUrl = i.Footer.HasValue ? i.Footer.Value.IconUrl : ""
                    };
                }

                if (i.Image.HasValue)
                {
                    var image = new Image
                    {
                        Url = i.Image.Value.Url
                    };
                    e.Image = image;
                }

                if (i.Thumbnail.HasValue)
                {
                    var thumbnail = new Thumbnail
                    {
                        Url = i.Thumbnail.Value.Url
                    };
                    e.Thumbnail = thumbnail;
                }

                if (i.Color.HasValue)
                {
                    e.Color = i.Color.Value.RawValue.ToString();
                }

                embedList.Add(e);
            }

            eb.Embeds.AddRange(embedList);

            if (message.Components.Any())
            {
                foreach (var e in message.Components)
                {
                    var actionComponents = e as ActionRowComponent;
                    foreach (var actionComponent in actionComponents.Components)
                    {
                        if (actionComponent is ButtonComponent button)
                        {
                            var component = new NewEmbed.NewEmbedComponent
                            {
                                Style = button.Style,
                                DisplayName = button.Label,
                                Url = button.Url,
                                Id = button.CustomId,
                                Emoji = button.Emote?.ToString()
                            };
                            componentList.Add(component);
                        }

                        if (actionComponent is not SelectMenuComponent select) continue;
                        {
                            var component = new NewEmbed.NewEmbedComponent
                            {
                                IsSelect = true, DisplayName = select.Placeholder, Id = select.CustomId, Options = new List<NewEmbed.NewEmbedSelectOption>()
                            };

                            var optionList = select.Options.Select(option => new NewEmbed.NewEmbedSelectOption
                                {
                                    Description = option.Description, Name = option.Label, Id = option.Value, Emoji = option.Emote?.ToString()
                                })
                                .ToList();

                            component.Options.AddRange(optionList);
                            componentList.Add(component);
                        }
                    }
                }
            }

            eb.Components.AddRange(componentList);
            return eb;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to get embed source");
            return null;
        }
    }
}