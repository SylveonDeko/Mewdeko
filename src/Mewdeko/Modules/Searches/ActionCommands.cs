using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using NekosBestApiNet;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    public class ActionCommands : MewdekoSubmodule
    {
        private readonly NekosBestApi _nekosBestApi;

        public ActionCommands(NekosBestApi nekosBestApi) => _nekosBestApi = nekosBestApi;
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Shoot(string toShow)
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
                Description = $"{ctx.User.Mention} shot {toShow}",
                ImageUrl = shootarray[index],
                Color = Mewdeko.ErrorColor
            };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description]
        public async Task Hug([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Hug();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} hugged {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build());
        }


        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Kiss([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Kiss();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} kissed {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Pat([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Pat();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} gave pattus to {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Tickle([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Tickle();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} tickles {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Slap([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Slap();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} slapped {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Cuddle([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Cuddle();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} cuddled with {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Poke([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Poke();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} poked {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Feed([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Feed();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} fed {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Baka([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Baka();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} says {toShow} is a baka", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Bite([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Bite();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} bites {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Blush([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Blush();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} blushes\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Bored([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Bored();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} is bored\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Cry([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Cry();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} cries\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Dance([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Dance();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} dances\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Facepalm([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Facepalm();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} facepalms\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Happy([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Happy();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} is happpy\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task HighFive([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Highfive();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} gives {toShow} a high-five", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Laugh([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Laugh();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} laughs\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Pout([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Pout();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} pouts\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Shrug([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Shrug();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} shrugs\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Sleep([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Sleep();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} sleeps\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Smile([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Smile();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} smiles\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Smug([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Smug();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} is smug\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Stare([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Stare();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} stares\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Think([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Think();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} thinks\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task ThumbsUp([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Thumbsup();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} gives a thumbsup\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Wave([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Wave();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} waves\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Wink([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Wink();
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} winks\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync("", embed: em.Build());
        }
    }
}