using System.IO;
using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Gambling.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Mewdeko.Modules.Gambling;

public partial class Gambling
{
    [Group]
    public class DrawCommands : MewdekoSubmodule
    {
        private static readonly ConcurrentDictionary<IGuild, Deck> AllDecks = new();
        private readonly IImageCache images;

        public DrawCommands(IDataCache data) => images = data.LocalImages;

        private async Task<(Stream ImageStream, string ToSend)> InternalDraw(int num, ulong? guildId = null)
        {
            if (num is < 1 or > 10)
                throw new ArgumentOutOfRangeException(nameof(num));

            var cards = guildId == null ? new Deck() : AllDecks.GetOrAdd(ctx.Guild, _ => new Deck());
            var list = new List<Image<Rgba32>>();
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
                list.Add(Image.Load<Rgba32>(this.images.GetCard(currentCard.ToString().ToLowerInvariant()
                    .Replace(' ', '_'))));
            }

            using var img = list.Merge();
            foreach (var i in list) i.Dispose();

            var toSend = $"{Format.Bold(ctx.User.ToString())}";
            if (cardObjects.Count == 5)
                toSend += $" drew `{Deck.GetHandValue(cardObjects)}`";

            if (guildId != null)
                toSend += $"\n{GetText("cards_left", Format.Bold(cards.CardPool.Count.ToString()))}";

            return (img.ToStream(), toSend);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Draw(int num = 1)
        {
            if (num < 1)
                num = 1;
            if (num > 10)
                num = 10;

            var (imageStream, toSend) = await InternalDraw(num, ctx.Guild.Id).ConfigureAwait(false);
            await using (imageStream)
            {
                await ctx.Channel.SendFileAsync(imageStream, $"{num} cards.jpg", toSend).ConfigureAwait(false);
            }
        }

        [Cmd, Aliases]
        public async Task DrawNew(int num = 1)
        {
            if (num < 1)
                num = 1;
            if (num > 10)
                num = 10;

            var (imageStream, toSend) = await InternalDraw(num).ConfigureAwait(false);
            await using (imageStream)
            {
                await ctx.Channel.SendFileAsync(imageStream, $"{num} cards.jpg", toSend).ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task DeckShuffle()
        {
            //var channel = (ITextChannel)ctx.Channel;

            AllDecks.AddOrUpdate(ctx.Guild,
                _ => new Deck(),
                (_, c) =>
                {
                    c.Restart();
                    return c;
                });

            await ReplyConfirmLocalizedAsync("deck_reshuffled").ConfigureAwait(false);
        }
    }
}