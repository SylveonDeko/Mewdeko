using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using KSoftNet;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Replacements;
using Mewdeko.Core.Modules.Searches.Common;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Interactive;
using Mewdeko.Interactive.Pagination;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Modules.Searches.Services;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using Configuration = AngleSharp.Configuration;

namespace Mewdeko.Modules.Searches
{
    public partial class Searches : MewdekoModule<SearchesService>
    {
        private static readonly ConcurrentDictionary<string, string> cachedShortenedLinks = new();
        private readonly IMemoryCache _cache;
        private readonly IBotCredentials _creds;
        private readonly IGoogleApiService _google;
        private readonly IHttpClientFactory _httpFactory;
        private readonly KSoftAPI _kSoftAPI;
        private readonly GuildTimezoneService _tzSvc;
        private InteractiveService Interactivity;

        public Searches(IBotCredentials creds, IGoogleApiService google, IHttpClientFactory factory, IMemoryCache cache,
            GuildTimezoneService tzSvc,
            KSoftAPI kSoftAPI, InteractiveService serv)
        {
            Interactivity = serv;
            _kSoftAPI = kSoftAPI;
            _creds = creds;
            _google = google;
            _httpFactory = factory;
            _cache = cache;
            _tzSvc = tzSvc;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        public async Task Hug(IUser user)
        {
            string[] gifs =
            {
                "http://media.tumblr.com/tumblr_m68m3wjllW1qewqw2.gif",
                "http://33.media.tumblr.com/e9fae5fd165029c63c6963f855238c1b/tumblr_ncq9kwWdjW1sy4pr2o1_500.gif",
                "http://38.media.tumblr.com/b22e5793e257faf94cec24ba034d46cd/tumblr_nldku9v7ar1ttpgxko1_500.gif",
                "http://25.media.tumblr.com/tumblr_mad9v0FbLA1r6bk7qo2_500.gif",
                "http://33.media.tumblr.com/510818c33b426e9ba73f809daec3f045/tumblr_n2bye1AaeQ1tv44eho1_500.gif",
                "http://25.media.tumblr.com/tumblr_m0fuxezxqu1ql02n0o1_500.gif",
                "http://static.tumblr.com/rgjkzhi/Qd5m7n1nj/tumblr_m7jbdcogab1qh0jlh.gif",
                "https://media.giphy.com/media/XEmhUht7W2CNq/giphy.gif",
                "http://mrwgifs.com/wp-content/uploads/2013/04/Ouran-High-School-Host-Club-Love-Hug-Gif.gif",
                "https://31.media.tumblr.com/e66b45dc71f2b4e29b9834034eba47cf/tumblr_mvo14g0ZX91rypg9ro1_500.gif",
                "http://media.giphy.com/media/s31WaGPAmTP1e/giphy.gif",
                "https://media.giphy.com/media/49mdjsMrH7oze/giphy.gif",
                "http://31.media.tumblr.com/tumblr_m2rq4kT2eY1qkb6keo1_500.gif",
                "http://25.media.tumblr.com/2a3ec53a742008eb61979af6b7148e8d/tumblr_mt1cllxlBr1s2tbc6o1_500.gif",
                "https://31.media.tumblr.com/57653f6ce67d5f96767c6642906a5c88/tumblr_n32xk1JA2G1tvuu8no1_500.gif",
                "http://i.imgur.com/1gvENc3.gif",
                "https://myanimelist.cdn-dena.com/s/common/uploaded_files/1461068486-646f3523d0fd8f3e6d818d96012b248e.gif",
                "https://33.media.tumblr.com/c0189fa705b0894dd12a0cb948064e14/tumblr_mn9608uJLM1sqb4xeo1_500.gif",
                "http://37.media.tumblr.com/66c19998360481a17ca928283006297c/tumblr_n4i4jvTWLe1sg0ygjo1_500.gif",
                "http://25.media.tumblr.com/668e4508190fb9f62ea9b5eb1d112531/tumblr_mw41ntelfK1s6ghcbo1_500.gif",
                "http://24.media.tumblr.com/ab7dd4617a37ed5b22606117f8428003/tumblr_n3ojd0CoI61txgib0o1_500.gif",
                "http://media.tumblr.com/tumblr_m1oqhy8vrH1qfwmvy.gif",
                "https://31.media.tumblr.com/5e86bb5906d5d5603351e9dbea007dea/tumblr_inline_n998n40b2q1sx8vac.gif",
                "http://i.imgur.com/GfSB94u.gif", "https://media.giphy.com/media/od5H3PmEG5EVq/giphy.gif",
                "http://media.giphy.com/media/143v0Z4767T15e/giphy.gif",
                "http://31.media.tumblr.com/c63a48856edab67f2e5c9b9c8a10d21e/tumblr_mkglr72JO61s7y044o1_500.gif",
                "http://media.tumblr.com/tumblr_mewo9gcfj21rvon2g.gif",
                "http://media.tumblr.com/tumblr_mabh68A9Xd1qfkm7e.gif",
                "http://25.media.tumblr.com/tumblr_m3d52tBC8H1rtrb2fo1_500.gif",
                "http://media3.giphy.com/media/DjczAlIcyK1Co/giphy.gif",
                "http://media.giphy.com/media/aD1fI3UUWC4/giphy.gif",
                "http://orig03.deviantart.net/648e/f/2013/071/2/3/hug_by_shiro_nee-d5xtm62.gif",
                "http://i.imgur.com/8ruodNJ.gif",
                "http://38.media.tumblr.com/91b578f5c95575f088f05be5ee0f284a/tumblr_n1ot2zAWgW1t8zoigo1_500.gif",
                "http://images6.fanpop.com/image/photos/32700000/Clannad-Gifs-clannad-32781225-500-281.gif",
                "https://myanimelist.cdn-dena.com/s/common/uploaded_files/1461071296-7451c05f5aae134e2cceb276b085a871.gif",
                "http://31.media.tumblr.com/a4119e7feb02c0094a6628e6b7cf3924/tumblr_mvqfcgI5eQ1s2p1gco1_500.gif",
                "http://31.media.tumblr.com/3f9570ee1019d79a5570b9017fecb601/tumblr_mpy92bYeVC1qbvovho1_500.gif",
                "http://i1.kym-cdn.com/photos/images/original/000/931/030/394.gif",
                "http://s5.favim.com/orig/140902/anime-anime-boy-anime-couple-anime-girl-Favim.com-2042715.gif",
                "https://31.media.tumblr.com/646e81a4d0b42d332507511b624cd9b5/tumblr_nhq1ggeWna1u3dv1jo2_500.gif",
                "http://i.imgur.com/rlOJqHL.gif",
                "http://s1.favim.com/orig/151126/anime-boys-gif-hug-Favim.com-3651141.gif",
                "http://i.imgur.com/I8LyQ9L.gif",
                "http://mrwgifs.com/wp-content/uploads/2013/05/Emotional-Hug-In-Castle-in-the-Sky-By-Studio-Ghibli.gif",
                "http://www.funnyjunk.com/Loli+jump/funny-gifs/4978690#13f1e6_4978246",
                "https://puu.sh/u7II6/75d544523b.gif", "https://puu.sh/u7ITp/fab20dc157.gif",
                "https://puu.sh/u7IV6/c75cd791d9.gif"
            };
            var rand = new Random();
            var index = rand.Next(gifs.Length);
            var em = new EmbedBuilder
            {
                Description = $"{ctx.User.Mention} hugged {user.Mention}",
                ImageUrl = gifs[index],
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }


        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Kiss(IUser user)
        {
            string[] gifs =
            {
                "https://cdn.discordapp.com/attachments/277519777449377794/282239845580013568/8R1n0hJ.jpg",
                "https://cdn.discordapp.com/attachments/277519777449377794/282240025310134272/anime-black-boy-girl-Favim.com-3733727.jpg",
                "https://cdn.discordapp.com/attachments/277519777449377794/282240073297035274/Black-And-White-Anime-Boy-Hard-Kiss-To-Girl-Wallpaper.jpg",
                "https://cdn.discordapp.com/attachments/277519777449377794/282240399735521281/40071-Anime-Kiss-Arte-De-La-Espada-En-Linea-Kirito-Asuna.gif",
                "https://cdn.discordapp.com/attachments/277519777449377794/282240428265439232/a5b892b2e5ea243029bdf25f48f6e16d.gif",
                "https://cdn.discordapp.com/attachments/277519777449377794/282240446216929282/anime-cat-chibi-cute-Favim.com-4722517.gif",
                "https://cdn.discordapp.com/attachments/277519777449377794/282240582057721866/lZ7gAES.gif",
                "https://cdn.discordapp.com/attachments/277519777449377794/282240644930338816/tumblr_static_filename_640_v2.gif",
                "http://i.myniceprofile.com/1512/151229.gif",
                "http://pa1.narvii.com/5822/71ef73672cd5dd6d53a2dde339bca15a2f9fc8da_hq.gif",
                "https://media.giphy.com/media/12VXIxKaIEarL2/giphy.gif",
                "http://68.media.tumblr.com/2b00d35e9237f5a456a864a7b7fdcadf/tumblr_mz5oxfRrVu1rx1dfqo1_r1_500.gif",
                "https://s-media-cache-ak0.pinimg.com/originals/e3/4e/31/e34e31123f8f35d5c771a2d6a70bef52.gif",
                "http://pa1.narvii.com/5848/0a46649e9e80c5c50459debb60ea827f32299416_hq.gif",
                "https://cdn.discordapp.com/attachments/274989585237540865/282241653165785088/tumblr_nb1ie95ezY1taeqnfo1_500.gif",
                "https://cdn.discordapp.com/attachments/274989585237540865/282241627714617344/tumblr_mdu5yoBhnZ1rhgchxo1_500.gif",
                "https://33.media.tumblr.com/e8228a8e3b323baf8fc9e79991780098/tumblr_n3w5vzg3KJ1qi1g2ao10_500.gif",
                "http://24.media.tumblr.com/tumblr_mdegsoUwmX1rxf1iro1_500.gif",
                "http://24.media.tumblr.com/5d51b3bbd64ccf1627dc87157a38e59f/tumblr_n5rfnvvj7H1t62gxao1_500.gif",
                "http://25.media.tumblr.com/0a969f0c8ac5972ad663fc1a14f4663b/tumblr_mgbyvzpksK1rmdepbo1_500.gif",
                "http://38.media.tumblr.com/84756421d21634f5f65d0d0f4c9da86f/tumblr_n2jz1jqEtq1sggrnxo1_500.gif",
                "http://media.giphy.com/media/vHISzfc8dcVG0/giphy.gif#524157",
                "http://37.media.tumblr.com/53bf74a951fc87a0cc15686f5aadb769/tumblr_n14rfuvCe41sc1kfto1_500.gif",
                "http://31.media.tumblr.com/f9e5fab28c5fd389fad6789c3b18aa6f/tumblr_mxhlrnCvPi1rveihgo1_400.gif",
                "http://i.myniceprofile.com/1486/148638.gif", "https://media.giphy.com/media/HSgkuMRab3fK8/giphy.gif",
                "http://24.media.tumblr.com/dc0496ce48c1c33182f24b1535521af2/tumblr_mqrl3ynedk1scihu7o1_500.gif",
                "http://media.giphy.com/media/oHZPerDaubltu/giphy.gif",
                "http://25.media.tumblr.com/tumblr_mcf25oETLX1r0ydwlo1_500.gif",
                "http://www.lovethisgif.com/uploaded_images/41239-Anime-Cheek-Kiss-Gif-Karen-Kissing-Shino-lewd-.gif",
                "http://i.mobofree.com/?u=http%3A%2F%2Fdata.whicdn.com%2Fimages%2F145488475%2Flarge.gif&w=600&h=1500",
                "http://25.media.tumblr.com/3b8a73c70947679a6af56178762bdc1f/tumblr_mk8xzkenY71qzd219o1_500.gifhttps://68.media.tumblr.com/4cfb137bb0c61101caff4ef661ef99b6/tumblr_nlyakoSeA31u7pak7o1_500.gif",
                "http://33.media.tumblr.com/tumblr_m7x4176tyH1ro4cfco1_500.gif",
                "https://media.giphy.com/media/Z2sivLSfN8FH2/giphy.gif",
                "http://anime-fanservice.org/coppermine/albums/K_galleries/Kiss_X_Sis_OAD/Kiss_X_Sis_OAD3-057.gif",
                "http://i.imgur.com/Wkf4BjA.gif", "https://media.giphy.com/media/ZRSGWtBJG4Tza/giphy.gif",
                "http://24.media.tumblr.com/d4f03ca449e3d51325e9ba0cc6a11b24/tumblr_mmjr3zHgmw1s6qc3bo1_500.gif",
                "http://media.giphy.com/media/X3ndlrK6rOCt2/giphy.gif",
                "https://33.media.tumblr.com/b79a72b3f50f32e0a9819250814711e0/tumblr_nrwyl4hss81uwoitwo1_500.gif",
                "http://24.media.tumblr.com/10bcb6c9307fc559e9d7fc45046c76d0/tumblr_ml7he1gYeU1snjuilo1_500.gif",
                "http://media.giphy.com/media/514rRMooEn8ti/giphy.gif",
                "http://media.tumblr.com/tumblr_mbawr10q0c1qfkm7e.gif",
                "https://elrefugiodelosincomprendidos.files.wordpress.com/2013/03/nodame-cantabile-kiss.gif",
                "http://media.giphy.com/media/zdY1ICmmWhW5a/giphy.gif",
                "https://31.media.tumblr.com/52e2cd6735ac92efb15be079fc06fe3b/tumblr_msr6hvvRY61sh31wjo1_500.gif",
                "http://i.imgur.com/hdZEXN3.gif",
                "http://orig00.deviantart.net/5abd/f/2012/054/8/9/gray_x_lucy_kiss_animation_by_milady666-d4qqqf2.gif",
                "https://media.giphy.com/media/EVODaJHSXZGta/giphy.gif", "http://anime-gx.com/_txgx0/1393257265138.gif",
                "http://s8.favim.com/orig/151119/akagami-no-shirayukihime-anime-boy-couple-Favim.com-3598058.gif",
                "https://66.media.tumblr.com/9ef04153773078d26e47e497821b4638/tumblr_nypr79WJRb1uuqo27o1_500.gif",
                "https://puu.sh/u7Ja8/9e59ab0bc7.gif", "https://i.gyazo.com/7d310f4eb9f3fb25b2fda2bd1657ef5b.gif"
            };
            var rand = new Random();
            var index = rand.Next(gifs.Length);
            var em = new EmbedBuilder
            {
                Description = $"{ctx.User.Mention} kissed {user.Mention}",
                ImageUrl = gifs[index],
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Pat(IUser user)
        {
            var patarray = new List<string>
            {
                "https://media3.giphy.com/media/ARSp9T7wwxNcs/200.gif",
                "https://media.tenor.com/images/da8431374a530ae516c0cc8f966d1c2b/tenor.gif",
                "https://media.tenor.com/images/ad8357e58d35c1d63b570ab7e587f212/tenor.gif",
                "https://media.tenor.com/images/a671268253717ff877474fd019ef73e9/tenor.gif",
                "https://media.tenor.com/images/ad8357e58d35c1d63b570ab7e587f212/tenor.gif",
                "https://media1.tenor.com/images/6151c42c94df654b1c7de2fdebaa6bd1/tenor.gif?itemid=16456868",
                "https://i.pinimg.com/originals/1a/94/61/1a9461f15bb5ae752680d99195f9b944.gif",
                "https://i.imgur.com/UWbKpx8.gif",
                "https://thumbs.gfycat.com/BlushingDeepBlacknorwegianelkhound-size_restricted.gif",
                "https://66.media.tumblr.com/a72dd82535f3e7accd827c202dacc09a/tumblr_pfyiqz0pFL1th206io1_640.gif",
                "https://i.imgur.com/2lacG7l.gif",
                "https://community.gamepress.gg/uploads/default/original/3X/7/e/7edae228fce7c05b1a1c9c27f78e0fb668ef2c65.gif",
                "https://i.pinimg.com/originals/c2/34/cd/c234cdcb3af7bed21ccbba2293470b8c.gif",
                "https://i.gifer.com/7MOk.gif",
                "https://66.media.tumblr.com/c078d8f0bd36f6d4520d9cb9f5c164ac/tumblr_p2kzogLOZU1vajq0ro7_500.gif",
                "https://i.pinimg.com/originals/ec/b8/7f/ecb87fb2827a022884d5165046f6608a.gif",
                "https://thumbs.gfycat.com/NauticalDampJerboa-max-1mb.gif",
                "https://media1.giphy.com/media/N0CIxcyPLputW/giphy.gif",
                "http://i.imgur.com/eUKM3Ay.gif",
                "https://i.imgur.com/Uq85eQI.gif",
                "https://i.imgur.com/ApevdXO.gif",
                "https://giffiles.alphacoders.com/932/93206.gif"
            };
            var rand = new Random();
            var index = rand.Next(patarray.Count);
            var em = new EmbedBuilder
            {
                Description = $"{ctx.User.Mention} gave pattus to {user.Mention}",
                ImageUrl = patarray[index],
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Shoot(IUser user)
        {
            var shootarray = new List<string>
            {
                "https://media.tenor.com/images/05085e9bc817361e783ad92a248ef318/tenor.gif",
                "https://media1.tenor.com/images/a0caaaec7f3f48fbcf037dd9e6a89c51/tenor.gif?itemid=12545029",
                "https://i.gifer.com/nin.gif",
                "https://i.imgflip.com/4fq6gm.gif",
                "https://cdn.myanimelist.net/s/common/uploaded_files/1448410154-7ba874393492485cf61797451b67a3be.gif",
                "https://thumbs.gfycat.com/DisguisedSimpleAmmonite-size_restricted.gif",
                "https://media0.giphy.com/media/a5OCMAro7MGQg/giphy.gif",
                "https://media1.tenor.com/images/e9f33b7ded139a73590878cf3f9d59a4/tenor.gif?itemid=16999058",
                "http://i.imgur.com/ygeo65P.gif",
                "https://gifimage.net/wp-content/uploads/2017/09/anime-shooting-gif-4.gif",
                "https://media0.giphy.com/media/rq8vsqrQmB128/giphy.gif",
                "https://pa1.narvii.com/6122/e688de863dc18f51f56cd5aabc677f7371a83701_hq.gif",
                "https://i2.wp.com/i.pinimg.com/originals/22/bb/ad/22bbade48e2ffa2c50968c635445b6a1.gif"
            };
            var rand = new Random();
            var index = rand.Next(shootarray.Count);
            var em = new EmbedBuilder
            {
                Description = $"{ctx.User.Mention} shot {user.Mention}",
                ImageUrl = shootarray[index],
                Color = Mewdeko.ErrorColor
            };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        //for anonymasen :^)
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Meme()
        {
            var msg = await ctx.Channel.SendConfirmAsync("Fetching random meme...");
            var image = await _kSoftAPI.imagesAPI.RandomMeme();
            while (_service.CheckIfAlreadyPosted(ctx.Guild, image.ImageUrl))
            {
                image = await _kSoftAPI.imagesAPI.RandomMeme();
            }
            var em = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = image.Author
                },
                Description = $"Title: {image.Title}\n[Source]({image.Source})",
                Footer = new EmbedFooterBuilder
                {
                    Text =
                        $"{image.Upvotes} Upvotes {image.Downvotes} Downvotes | {image.Subreddit} Powered by Ksoft.Si"
                },
                ImageUrl = image.ImageUrl,
                Color = Mewdeko.OkColor
            };
            await msg.ModifyAsync(x => x.Embed = em.Build());        
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task RandomAww()
        {
            var image = await _kSoftAPI.imagesAPI.RandomAww();
            var em = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = image.Author
                },
                Description = $"Title: {image.Title}\n[Source]({image.Source})",
                Footer = new EmbedFooterBuilder
                {
                    Text =
                        $"{image.Upvotes} Upvotes {image.Downvotes} Downvotes | {image.Subreddit} Powered by Ksoft.Si"
                },
                ImageUrl = image.ImageUrl,
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task RandomReddit(string subreddit)
        {
            var msg = await ctx.Channel.SendConfirmAsync("Checking if the subreddit is nsfw...");
            if(_service.NsfwCheck(subreddit))
            {
                var emt = new EmbedBuilder()
                {
                    Description = "This subreddit is nsfw!",
                    Color = Mewdeko.ErrorColor
                };
                await msg.ModifyAsync(x => x.Embed = emt.Build());
                return;
            }
            var image = await _kSoftAPI.imagesAPI.RandomReddit(subreddit, true, "year");
            while(_service.CheckIfAlreadyPosted(ctx.Guild, image.ImageUrl))
            {
                image = await _kSoftAPI.imagesAPI.RandomReddit(subreddit, true, "year");
            }
            var em = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = image.Author
                },
                Description = $"Title: {image.Title}\n[Source]({image.Source})",
                Footer = new EmbedFooterBuilder
                {
                    Text = $"{image.Upvotes} Upvotes! | {image.Subreddit} Powered by KSoft.si"
                },
                ImageUrl = image.ImageUrl,
                Color = Mewdeko.OkColor
            };
            await msg.ModifyAsync(x => x.Embed = em.Build());
        }

        //for anonymasen :^)
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Rip([Leftover] IGuildUser usr)
        {
            var av = usr.RealAvatarUrl();
            if (av == null)
                return;
            using (var picStream =
                await _service.GetRipPictureAsync(usr.Nickname ?? usr.Username, av).ConfigureAwait(false))
            {
                await ctx.Channel.SendFileAsync(
                        picStream,
                        "rip.png",
                        $"Rip {Format.Bold(usr.ToString())} \n\t- " +
                        Format.Italics(ctx.User.ToString()))
                    .ConfigureAwait(false);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageMessages)]
        [Priority(1)]
        public async Task Say(ITextChannel channel, [Leftover] string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var rep = new ReplacementBuilder()
                .WithDefault(ctx.User, channel, (SocketGuild) ctx.Guild, (DiscordSocketClient) ctx.Client)
                .Build();

            if (CREmbed.TryParse(message, out var embedData))
            {
                rep.Replace(embedData);
                await channel.EmbedAsync(embedData, !((IGuildUser) Context.User).GuildPermissions.MentionEveryone)
                    .ConfigureAwait(false);
            }
            else
            {
                var msg = rep.Replace(message);
                if (!string.IsNullOrWhiteSpace(msg)) await channel.SendConfirmAsync(msg).ConfigureAwait(false);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageMessages)]
        [Priority(0)]
        public Task Say([Leftover] string message)
        {
            return Say((ITextChannel) ctx.Channel, message);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Weather([Leftover] string query)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            var embed = new EmbedBuilder();
            var data = await _service.GetWeatherDataAsync(query).ConfigureAwait(false);

            if (data == null)
            {
                embed.WithDescription(GetText("city_not_found"))
                    .WithErrorColor();
            }
            else
            {
                Func<double, double> f = StandardConversions.CelsiusToFahrenheit;

                var tz = Context.Guild is null
                    ? TimeZoneInfo.Utc
                    : _tzSvc.GetTimeZoneOrUtc(Context.Guild.Id);
                var sunrise = data.Sys.Sunrise.ToUnixTimestamp();
                var sunset = data.Sys.Sunset.ToUnixTimestamp();
                sunrise = sunrise.ToOffset(tz.GetUtcOffset(sunrise));
                sunset = sunset.ToOffset(tz.GetUtcOffset(sunset));
                var timezone = $"UTC{sunrise:zzz}";

                embed.AddField(fb =>
                        fb.WithName("🌍 " + Format.Bold(GetText("location")))
                            .WithValue(
                                $"[{data.Name + ", " + data.Sys.Country}](https://openweathermap.org/city/{data.Id})")
                            .WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName("📏 " + Format.Bold(GetText("latlong")))
                            .WithValue($"{data.Coord.Lat}, {data.Coord.Lon}").WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName("☁ " + Format.Bold(GetText("condition")))
                            .WithValue(string.Join(", ", data.Weather.Select(w => w.Main))).WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName("😓 " + Format.Bold(GetText("humidity"))).WithValue($"{data.Main.Humidity}%")
                            .WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName("💨 " + Format.Bold(GetText("wind_speed"))).WithValue(data.Wind.Speed + " m/s")
                            .WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName("🌡 " + Format.Bold(GetText("temperature")))
                            .WithValue($"{data.Main.Temp:F1}°C / {f(data.Main.Temp):F1}°F").WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName("🔆 " + Format.Bold(GetText("min_max")))
                            .WithValue(
                                $"{data.Main.TempMin:F1}°C - {data.Main.TempMax:F1}°C\n{f(data.Main.TempMin):F1}°F - {f(data.Main.TempMax):F1}°F")
                            .WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName("🌄 " + Format.Bold(GetText("sunrise"))).WithValue($"{sunrise:HH:mm} {timezone}")
                            .WithIsInline(true))
                    .AddField(fb =>
                        fb.WithName("🌇 " + Format.Bold(GetText("sunset"))).WithValue($"{sunset:HH:mm} {timezone}")
                            .WithIsInline(true))
                    .WithOkColor()
                    .WithFooter(efb =>
                        efb.WithText("Powered by openweathermap.org")
                            .WithIconUrl($"http://openweathermap.org/img/w/{data.Weather[0].Icon}.png"));
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Time([Leftover] string query)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

            var (data, err) = await _service.GetTimeDataAsync(query).ConfigureAwait(false);
            if (!(err is null))
            {
                string errorKey;
                switch (err)
                {
                    case TimeErrors.ApiKeyMissing:
                        errorKey = "api_key_missing";
                        break;
                    case TimeErrors.InvalidInput:
                        errorKey = "invalid_input";
                        break;
                    case TimeErrors.NotFound:
                        errorKey = "not_found";
                        break;
                    default:
                        errorKey = "error_occured";
                        break;
                }

                await ReplyErrorLocalizedAsync(errorKey).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(data.TimeZoneName))
            {
                await ReplyErrorLocalizedAsync("timezone_db_api_key").ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(GetText("time_new"))
                .WithDescription(Format.Code(data.Time.ToString()))
                .AddField(GetText("location"), string.Join('\n', data.Address.Split(", ")), true)
                .AddField(GetText("timezone"), data.TimeZoneName, true);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Youtube([Leftover] string query = null)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            var result = (await _google.GetVideoLinksByKeywordAsync(query).ConfigureAwait(false)).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(result))
            {
                await ReplyErrorLocalizedAsync("no_results").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.SendMessageAsync(result).ConfigureAwait(false);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Movie([Leftover] string query = null)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

            var movie = await _service.GetMovieDataAsync(query).ConfigureAwait(false);
            if (movie == null)
            {
                await ReplyErrorLocalizedAsync("imdb_fail").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle(movie.Title)
                .WithUrl($"http://www.imdb.com/title/{movie.ImdbId}/")
                .WithDescription(movie.Plot.TrimTo(1000))
                .AddField(efb => efb.WithName("Rating").WithValue(movie.ImdbRating).WithIsInline(true))
                .AddField(efb => efb.WithName("Genre").WithValue(movie.Genre).WithIsInline(true))
                .AddField(efb => efb.WithName("Year").WithValue(movie.Year).WithIsInline(true))
                .WithImageUrl(movie.Poster)).ConfigureAwait(false);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public Task RandomCat()
        {
            return InternalRandomImage(SearchesService.ImageTag.Cats);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public Task RandomDog()
        {
            return InternalRandomImage(SearchesService.ImageTag.Dogs);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public Task RandomFood()
        {
            return InternalRandomImage(SearchesService.ImageTag.Food);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public Task RandomBird()
        {
            return InternalRandomImage(SearchesService.ImageTag.Birds);
        }

        // done in 3.0
        private Task InternalRandomImage(SearchesService.ImageTag tag)
        {
            var url = _service.GetRandomImageUrl(tag);
            return ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithImageUrl(url));
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Image([Leftover] string query = null)
        {
            var oterms = query?.Trim();
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;
            query = WebUtility.UrlEncode(oterms).Replace(' ', '+');
            try
            {
                var res = await _google.GetImageAsync(oterms).ConfigureAwait(false);
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithAuthor(eab => eab.WithName(GetText("image_search_for") + " " + oterms.TrimTo(50))
                        .WithUrl("https://www.google.com/search?q=" + query + "&source=lnms&tbm=isch")
                        .WithIconUrl("http://i.imgur.com/G46fm8J.png"))
                    .WithDescription(res.Link)
                    .WithImageUrl(res.Link)
                    .WithTitle(ctx.User.ToString());
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            catch
            {
                Log.Warning("Falling back to Imgur");

                var fullQueryLink = $"http://imgur.com/search?q={query}";
                var config = Configuration.Default.WithDefaultLoader();
                using (var document = await BrowsingContext.New(config).OpenAsync(fullQueryLink).ConfigureAwait(false))
                {
                    var elems = document.QuerySelectorAll("a.image-list-link").ToList();

                    if (!elems.Any())
                        return;

                    var img = elems.ElementAtOrDefault(new MewdekoRandom().Next(0, elems.Count))?.Children
                        ?.FirstOrDefault() as IHtmlImageElement;

                    if (img?.Source == null)
                        return;

                    var source = img.Source.Replace("b.", ".", StringComparison.InvariantCulture);

                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(eab => eab.WithName(GetText("image_search_for") + " " + oterms.TrimTo(50))
                            .WithUrl(fullQueryLink)
                            .WithIconUrl("http://s.imgur.com/images/logo-1200-630.jpg?"))
                        .WithDescription(source)
                        .WithImageUrl(source)
                        .WithTitle(ctx.User.ToString());
                    await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Lmgtfy([Leftover] string ffs = null)
        {
            if (!await ValidateQuery(ctx.Channel, ffs).ConfigureAwait(false))
                return;

            await ctx.Channel.SendConfirmAsync("<" +
                                               await _google
                                                   .ShortenUrl($"http://lmgtfy.com/?q={Uri.EscapeUriString(ffs)}")
                                                   .ConfigureAwait(false) + ">")
                .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Shorten([Leftover] string query)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            query = query.Trim();
            if (!cachedShortenedLinks.TryGetValue(query, out var shortLink))
                try
                {
                    using (var _http = _httpFactory.CreateClient())
                    using (var req = new HttpRequestMessage(HttpMethod.Post, "https://goolnk.com/api/v1/shorten"))
                    {
                        var formData = new MultipartFormDataContent
                        {
                            {new StringContent(query), "url"}
                        };
                        req.Content = formData;

                        using (var res = await _http.SendAsync(req).ConfigureAwait(false))
                        {
                            var content = await res.Content.ReadAsStringAsync();
                            var data = JsonConvert.DeserializeObject<ShortenData>(content);

                            if (!string.IsNullOrWhiteSpace(data?.ResultUrl))
                                cachedShortenedLinks.TryAdd(query, data.ResultUrl);
                            else
                                return;

                            shortLink = data.ResultUrl;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error shortening a link: {Message}", ex.Message);
                    return;
                }

            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithColor(Mewdeko.OkColor)
                    .AddField(efb => efb.WithName(GetText("original_url"))
                        .WithValue($"<{query}>"))
                    .AddField(efb => efb.WithName(GetText("short_url"))
                        .WithValue($"<{shortLink}>")))
                .ConfigureAwait(false);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Google([Leftover] string query = null)
        {
            query = query?.Trim();
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            _ = ctx.Channel.TriggerTypingAsync();

            var data = await _service.GoogleSearchAsync(query);
            if (data is null)
            {
                await ReplyErrorLocalizedAsync("no_results");
                return;
            }

            var desc = data.Results.Take(5).Select(res =>
                $@"[**{res.Title}**]({res.Link})
{res.Text.TrimTo(400 - res.Title.Length - res.Link.Length)}");

            var descStr = string.Join("\n\n", desc);

            var embed = new EmbedBuilder()
                .WithAuthor(eab => eab.WithName(GetText("search_for") + " " + query.TrimTo(50))
                    .WithUrl(data.FullQueryLink)
                    .WithIconUrl("http://i.imgur.com/G46fm8J.png"))
                .WithTitle(ctx.User.ToString())
                .WithFooter(efb => efb.WithText(data.TotalResults))
                .WithDescription(descStr)
                .WithOkColor();

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }


        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task MagicTheGathering([Leftover] string search)
        {
            if (!await ValidateQuery(ctx.Channel, search))
                return;

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var card = await _service.GetMtgCardAsync(search).ConfigureAwait(false);

            if (card == null)
            {
                await ReplyErrorLocalizedAsync("card_not_found").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(card.Name)
                .WithDescription(card.Description)
                .WithImageUrl(card.ImageUrl)
                .AddField(efb => efb.WithName(GetText("store_url")).WithValue(card.StoreUrl).WithIsInline(true))
                .AddField(efb => efb.WithName(GetText("cost")).WithValue(card.ManaCost).WithIsInline(true))
                .AddField(efb => efb.WithName(GetText("types")).WithValue(card.Types).WithIsInline(true));

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Hearthstone([Leftover] string name)
        {
            var arg = name;
            if (!await ValidateQuery(ctx.Channel, name).ConfigureAwait(false))
                return;

            if (string.IsNullOrWhiteSpace(_creds.MashapeKey))
            {
                await ReplyErrorLocalizedAsync("mashape_api_missing").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var card = await _service.GetHearthstoneCardDataAsync(name).ConfigureAwait(false);

            if (card == null)
            {
                await ReplyErrorLocalizedAsync("card_not_found").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithImageUrl(card.Img);

            if (!string.IsNullOrWhiteSpace(card.Flavor))
                embed.WithDescription(card.Flavor);

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task UrbanDict([Leftover] string query = null)
        {
            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            using (var http = _httpFactory.CreateClient())
            {
                var res = await http
                    .GetStringAsync($"http://api.urbandictionary.com/v0/define?term={Uri.EscapeUriString(query)}")
                    .ConfigureAwait(false);
                try
                {
                    var items = JsonConvert.DeserializeObject<UrbanResponse>(res).List;
                    if (items.Any())
                    {
                        var paginator = new LazyPaginatorBuilder()
                            .AddUser(ctx.User)
                            .WithPageFactory(PageFactory)
                            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                            .WithMaxPageIndex(items.Length - 1)
                            .WithDefaultEmotes()
                            .Build();

                        await Interactivity.SendPaginatorAsync(paginator, Context.Channel, System.TimeSpan.FromMinutes(60));

                        Task<PageBuilder> PageFactory(int page)
                        {

                            var item = items[page];
                            return Task.FromResult(new PageBuilder().WithOkColor()
                                .WithUrl(item.Permalink)
                                .WithAuthor(
                                    eab => eab.WithIconUrl("http://i.imgur.com/nwERwQE.jpg").WithName(item.Word))
                                .WithDescription(item.Definition));
                        }
                    }
                }
                catch
                {
                }
            }

            await ReplyErrorLocalizedAsync("ud_error").ConfigureAwait(false);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Define([Leftover] string word)
        {
            if (!await ValidateQuery(ctx.Channel, word).ConfigureAwait(false))
                return;

            using (var _http = _httpFactory.CreateClient())
            {
                string res;
                try
                {
                    res = await _cache.GetOrCreateAsync($"define_{word}", e =>
                    {
                        e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
                        return _http.GetStringAsync("https://api.pearson.com/v2/dictionaries/entries?headword=" +
                                                    WebUtility.UrlEncode(word));
                    }).ConfigureAwait(false);

                    var data = JsonConvert.DeserializeObject<DefineModel>(res);

                    var datas = data.Results
                        .Where(x => !(x.Senses is null) && x.Senses.Count > 0 && !(x.Senses[0].Definition is null))
                        .Select(x => (Sense: x.Senses[0], x.PartOfSpeech));

                    if (!datas.Any())
                    {
                        Log.Warning("Definition not found: {Word}", word);
                        await ReplyErrorLocalizedAsync("define_unknown").ConfigureAwait(false);
                    }


                    var col = datas.Select(data => (
                        Definition: data.Sense.Definition is string
                            ? data.Sense.Definition.ToString()
                            : ((JArray) JToken.Parse(data.Sense.Definition.ToString())).First.ToString(),
                        Example: data.Sense.Examples is null || data.Sense.Examples.Count == 0
                            ? string.Empty
                            : data.Sense.Examples[0].Text,
                        Word: word,
                        WordType: string.IsNullOrWhiteSpace(data.PartOfSpeech) ? "-" : data.PartOfSpeech
                    )).ToList();

                    Log.Information($"Sending {col.Count} definition for: {word}");

                    var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(col.Count() -1)
                        .WithDefaultEmotes()
                        .Build();

                    await Interactivity.SendPaginatorAsync(paginator, Context.Channel, System.TimeSpan.FromMinutes(60));

                    Task<PageBuilder> PageFactory(int page)
                    {
                        {
                            var data = col.Skip(page).First();
                            var embed = new PageBuilder()
                                .WithDescription(ctx.User.Mention)
                                .AddField(GetText("word"), data.Word, true)
                                .AddField(GetText("class"), data.WordType, true)
                                .AddField(GetText("definition"), data.Definition)
                                .WithOkColor();

                            if (!string.IsNullOrWhiteSpace(data.Example))
                                embed.AddField(efb => efb.WithName(GetText("example")).WithValue(data.Example));

                            return Task.FromResult(embed);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error retrieving definition data for: {Word}", word);
                }
            }
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Catfact()
        {
            using (var http = _httpFactory.CreateClient())
            {
                var response = await http.GetStringAsync("https://catfact.ninja/fact").ConfigureAwait(false);
                if (response == null)
                    return;

                var fact = JObject.Parse(response)["fact"].ToString();
                await ctx.Channel.SendConfirmAsync("🐈" + GetText("catfact"), fact).ConfigureAwait(false);
            }
        }

        //done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Revav([Leftover] IGuildUser usr = null)
        {
            if (usr == null)
                usr = (IGuildUser) ctx.User;

            var av = usr.RealAvatarUrl();
            if (av == null)
                return;

            await ctx.Channel.SendConfirmAsync($"https://images.google.com/searchbyimage?image_url={av}")
                .ConfigureAwait(false);
        }

        //done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Revimg([Leftover] string imageLink = null)
        {
            imageLink = imageLink?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(imageLink))
                return;
            await ctx.Channel.SendConfirmAsync($"https://images.google.com/searchbyimage?image_url={imageLink}")
                .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public Task Safebooru([Leftover] string tag = null)
        {
            return InternalDapiCommand(ctx.Message, tag, DapiSearchType.Safebooru);
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Wiki([Leftover] string query = null)
        {
            query = query?.Trim();

            if (!await ValidateQuery(ctx.Channel, query).ConfigureAwait(false))
                return;

            using (var http = _httpFactory.CreateClient())
            {
                var result = await http
                    .GetStringAsync(
                        "https://en.wikipedia.org//w/api.php?action=query&format=json&prop=info&redirects=1&formatversion=2&inprop=url&titles=" +
                        Uri.EscapeDataString(query)).ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<WikipediaApiModel>(result);
                if (data.Query.Pages[0].Missing || string.IsNullOrWhiteSpace(data.Query.Pages[0].FullUrl))
                    await ReplyErrorLocalizedAsync("wiki_page_not_found").ConfigureAwait(false);
                else
                    await ctx.Channel.SendMessageAsync(data.Query.Pages[0].FullUrl).ConfigureAwait(false);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Color(params Color[] colors)
        {
            if (!colors.Any())
                return;

            var colorObjects = colors.Take(10)
                .ToArray();

            using (var img = new Image<Rgba32>(colorObjects.Length * 50, 50))
            {
                for (var i = 0; i < colorObjects.Length; i++)
                {
                    var x = i * 50;
                    img.Mutate(m => m.FillPolygon(colorObjects[i], new PointF(x, 0), new PointF(x + 50, 0),
                        new PointF(x + 50, 50), new PointF(x, 50)));
                }

                using (var ms = img.ToStream())
                {
                    await ctx.Channel.SendFileAsync(ms, "colors.png").ConfigureAwait(false);
                }
            }
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Avatar([Remainder] IGuildUser usr = null)
        {
            if (usr == null)
                usr = (IGuildUser) ctx.User;

            var avatarUrl = usr.GetAvatarUrl(ImageFormat.Auto, 2048);

            if (avatarUrl == null)
            {
                await ReplyErrorLocalizedAsync("avatar_none", usr.ToString()).ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .AddField(efb => efb.WithName("Username").WithValue(usr.ToString()).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName("Avatar Url").WithValue("[Link]" + "(" + avatarUrl + ")").WithIsInline(true))
                .WithImageUrl(avatarUrl));
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Wikia(string target, [Leftover] string query)
        {
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(query))
            {
                await ReplyErrorLocalizedAsync("wikia_input_error").ConfigureAwait(false);
                return;
            }

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            using (var http = _httpFactory.CreateClient())
            {
                http.DefaultRequestHeaders.Clear();
                try
                {
                    var res = await http.GetStringAsync($"https://{Uri.EscapeUriString(target)}.fandom.com/api.php" +
                                                        "?action=query" +
                                                        "&format=json" +
                                                        "&list=search" +
                                                        $"&srsearch={Uri.EscapeUriString(query)}" +
                                                        "&srlimit=1").ConfigureAwait(false);
                    var items = JObject.Parse(res);
                    var title = items["query"]?["search"]?.FirstOrDefault()?["title"]?.ToString();

                    if (string.IsNullOrWhiteSpace(title))
                    {
                        await ReplyErrorLocalizedAsync("wikia_error").ConfigureAwait(false);
                        return;
                    }

                    var url = Uri.EscapeUriString($"https://{target}.fandom.com/wiki/{title}");
                    var response = $@"`{GetText("title")}` {title?.SanitizeMentions()}
`{GetText("url")}:` {url}";
                    await ctx.Channel.SendMessageAsync(response).ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalizedAsync("wikia_error").ConfigureAwait(false);
                }
            }
        }

        // done in 3.0
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Bible(string book, string chapterAndVerse)
        {
            var obj = new BibleVerses();
            try
            {
                using (var http = _httpFactory.CreateClient())
                {
                    var res = await http
                        .GetStringAsync("https://bible-api.com/" + book + " " + chapterAndVerse).ConfigureAwait(false);

                    obj = JsonConvert.DeserializeObject<BibleVerses>(res);
                }
            }
            catch
            {
            }

            if (obj.Error != null || obj.Verses == null || obj.Verses.Length == 0)
            {
                await ctx.Channel.SendErrorAsync(obj.Error ?? "No verse found.").ConfigureAwait(false);
            }
            else
            {
                var v = obj.Verses[0];
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"{v.BookName} {v.Chapter}:{v.Verse}")
                    .WithDescription(v.Text)).ConfigureAwait(false);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Steam([Leftover] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            var appId = await _service.GetSteamAppIdByName(query).ConfigureAwait(false);
            if (appId == -1)
            {
                await ReplyErrorLocalizedAsync("not_found").ConfigureAwait(false);
                return;
            }

            //var embed = new EmbedBuilder()
            //    .WithOkColor()
            //    .WithDescription(gameData.ShortDescription)
            //    .WithTitle(gameData.Name)
            //    .WithUrl(gameData.Link)
            //    .WithImageUrl(gameData.HeaderImage)
            //    .AddField(efb => efb.WithName(GetText("genres")).WithValue(gameData.TotalEpisodes.ToString()).WithIsInline(true))
            //    .AddField(efb => efb.WithName(GetText("price")).WithValue(gameData.IsFree ? GetText("FREE") : game).WithIsInline(true))
            //    .AddField(efb => efb.WithName(GetText("links")).WithValue(gameData.GetGenresString()).WithIsInline(true))
            //    .WithFooter(efb => efb.WithText(GetText("recommendations", gameData.TotalRecommendations)));
            await ctx.Channel.SendMessageAsync($"https://store.steampowered.com/app/{appId}").ConfigureAwait(false);
        }

        public async Task InternalDapiCommand(IUserMessage umsg, string tag, DapiSearchType type)
        {
            var channel = umsg.Channel;

            tag = tag?.Trim() ?? "";

            var imgObj = await _service.DapiSearch(tag, type, ctx.Guild?.Id).ConfigureAwait(false);

            if (imgObj == null)
                await channel.SendErrorAsync(umsg.Author.Mention + " " + GetText("no_results")).ConfigureAwait(false);
            else
                await channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithDescription($"{umsg.Author.Mention} [{tag ?? "url"}]({imgObj.FileUrl})")
                    .WithImageUrl(imgObj.FileUrl)
                    .WithFooter(efb => efb.WithText(type.ToString()))).ConfigureAwait(false);
        }

        public async Task<bool> ValidateQuery(IMessageChannel ch, string query)
        {
            if (!string.IsNullOrWhiteSpace(query)) return true;

            await ErrorLocalizedAsync("specify_search_params").ConfigureAwait(false);
            return false;
        }

        public class ShortenData
        {
            [JsonProperty("result_url")] public string ResultUrl { get; set; }
        }
    }
}