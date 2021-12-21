using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Services;
using Mewdeko.Modules.Gambling.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Mewdeko.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class DrawCommands : MewdekoSubmodule
        {
            private static readonly ConcurrentDictionary<IGuild, Deck> _allDecks = new();
            private readonly IImageCache _images;

            public DrawCommands(IDataCache data)
            {
                _images = data.LocalImages;
            }

            private async Task<(Stream ImageStream, string ToSend)> InternalDraw(int num, ulong? guildId = null)
            {
                if (num < 1 || num > 10)
                    throw new ArgumentOutOfRangeException(nameof(num));

                var cards = guildId == null ? new Deck() : _allDecks.GetOrAdd(ctx.Guild, s => new Deck());
                var images = new List<Image<Rgba32>>();
                var cardObjects = new List<Deck.Card>();
                for (var i = 0; i < num; i++)
                {
                    if (cards.CardPool.Count == 0 && i != 0)
                    {
                        try
                        {
                            await ReplyErrorLocalizedAsync("no_more_cards").ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        break;
                    }

                    var currentCard = cards.Draw();
                    cardObjects.Add(currentCard);
                    images.Add(Image.Load(_images.GetCard(currentCard.ToString().ToLowerInvariant()
                        .Replace(' ', '_'))));
                }

                using var img = images.Merge();
                foreach (var i in images) i.Dispose();

                var toSend = $"{Format.Bold(ctx.User.ToString())}";
                if (cardObjects.Count == 5)
                    toSend += $" drew `{Deck.GetHandValue(cardObjects)}`";

                if (guildId != null)
                    toSend += "\n" + GetText("cards_left", Format.Bold(cards.CardPool.Count.ToString()));

                return (img.ToStream(), toSend);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Draw(int num = 1)
            {
                if (num < 1)
                    num = 1;
                if (num > 10)
                    num = 10;

                var (ImageStream, ToSend) = await InternalDraw(num, ctx.Guild.Id).ConfigureAwait(false);
                await using (ImageStream)
                {
                    await ctx.Channel.SendFileAsync(ImageStream, num + " cards.jpg", ToSend).ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task DrawNew(int num = 1)
            {
                if (num < 1)
                    num = 1;
                if (num > 10)
                    num = 10;

                var (ImageStream, ToSend) = await InternalDraw(num).ConfigureAwait(false);
                await using (ImageStream)
                {
                    await ctx.Channel.SendFileAsync(ImageStream, num + " cards.jpg", ToSend).ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task DeckShuffle()
            {
                //var channel = (ITextChannel)ctx.Channel;

                _allDecks.AddOrUpdate(ctx.Guild,
                    g => new Deck(),
                    (g, c) =>
                    {
                        c.Restart();
                        return c;
                    });

                await ReplyConfirmLocalizedAsync("deck_reshuffled").ConfigureAwait(false);
            }
        }
    }
}