using Discord.Commands;
using NadekoBot.Extensions;
using System;
using System.Threading.Tasks;
using NadekoBot.Common;
using NadekoBot.Common.Attributes;

namespace NadekoBot.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class PlaceCommands : NadekoSubmodule
        {
            private static readonly string _typesStr = 
                string.Join(", ", Enum.GetNames(typeof(PlaceType)));

            public enum PlaceType
            {
                Cage, //http://www.placecage.com
                Steven, //http://www.stevensegallery.com
                Beard, //http://placebeard.it
                Fill, //http://www.fillmurray.com
                Bear, //https://www.placebear.com
                Kitten, //http://placekitten.com
                Bacon, //http://baconmockup.com
                Xoart, //http://xoart.link
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task Placelist()
            {
                await ctx.Channel.SendConfirmAsync(GetText("list_of_place_tags", Prefix), 
                    _typesStr)
                             .ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task Place(PlaceType placeType, uint width = 0, uint height = 0)
            {
                var url = "";
                switch (placeType)
                {
                    case PlaceType.Cage:
                        url = "http://www.placecage.com";
                        break;
                    case PlaceType.Steven:
                        url = "http://www.stevensegallery.com";
                        break;
                    case PlaceType.Beard:
                        url = "http://placebeard.it";
                        break;
                    case PlaceType.Fill:
                        url = "http://www.fillmurray.com";
                        break;
                    case PlaceType.Bear:
                        url = "https://www.placebear.com";
                        break;
                    case PlaceType.Kitten:
                        url = "http://placekitten.com";
                        break;
                    case PlaceType.Bacon:
                        url = "http://baconmockup.com";
                        break;
                    case PlaceType.Xoart:
                        url = "http://xoart.link";
                        break;
                }
                var rng = new NadekoRandom();
                if (width <= 0 || width > 1000)
                    width = (uint)rng.Next(250, 850);

                if (height <= 0 || height > 1000)
                    height = (uint)rng.Next(250, 850);

                url += $"/{width}/{height}";

                await ctx.Channel.SendMessageAsync(url).ConfigureAwait(false);
            }
        }
    }
}
