using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using NekosBestApiNet;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    public class ActionCommands : MewdekoSubmodule
    {
        private readonly NekosBestApi _nekosBestApi;

        public ActionCommands(NekosBestApi nekosBestApi) => _nekosBestApi = nekosBestApi;

        [Cmd, Aliases]
        public async Task Shoot(string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Shoot().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} shot {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }
        
        [Cmd, Aliases]
        public async Task Handhold(string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Handhold().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} handholded {toShow}\n\n Lewd!", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }
        
        [Cmd, Aliases]
        public async Task Punch(string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Punch().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} punched {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Hug([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Hug().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} hugged {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Kiss([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Kiss().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} kissed {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Pat([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Pat().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} gave pattus to {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Tickle([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Tickle().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} tickles {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Slap([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Slap().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} slapped {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Cuddle([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Cuddle().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} cuddled with {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Poke([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Poke().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} poked {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Feed([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Feed().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} fed {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Baka([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Baka().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} says {toShow} is a baka", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Bite([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Bite().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} bites {toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Blush([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Blush().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} blushes\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Bored([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Bored().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} is bored\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Cry([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Cry().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} cries\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Dance([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Dance().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} dances\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Facepalm([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Facepalm().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} facepalms\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Happy([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Happy().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} is happpy\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task HighFive([Remainder] string toShow)
        {
            var req = await _nekosBestApi.ActionsApi.Highfive().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} gives {toShow} a high-five", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Laugh([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Laugh().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} laughs\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Pout([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Pout().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} pouts\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Shrug([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Shrug().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} shrugs\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Sleep([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Sleep().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} sleeps\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Smile([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Smile().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} smiles\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Smug([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Smug().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} is smug\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Stare([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Stare().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} stares\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Think([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Think().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} thinks\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task ThumbsUp([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Thumbsup().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} gives a thumbsup\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Wave([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Wave().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} waves\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Wink([Remainder] string toShow = null)
        {
            var req = await _nekosBestApi.ActionsApi.Wink().ConfigureAwait(false);
            var em = new EmbedBuilder { Description = $"{ctx.User.Mention} winks\n{toShow}", ImageUrl = req.Results.FirstOrDefault().Url, Color = Mewdeko.OkColor };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }
    }
}