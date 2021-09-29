// using System;
// using Mewdeko.Extensions;
// using System.Linq;
// using Discord;
// using System.Text.RegularExpressions;
// using System.Threading.Tasks;
// using Discord.Commands;
// using Mewdeko.Modules.SwitchUtils.Services;
// using Mewdeko.Common.Attributes;

// namespace Mewdeko.Modules.SwitchUtils
// {
//     public partial class SwitchUtils
//     {
//         public class SwitchShop : MewdekoSubmodule<SwitchShopService>
//         {
//             [MewdekoCommand]
//             [RequireContext((ContextType.Guild))]
//             public async Task RequestShopAdd()
//             {
//                 var msg = await ctx.Channel.SendConfirmAsync("Please input your shop name");
//                 var e = await GetUserInputAsync(ctx.User.Id, ctx.Channel.Id);
//                 if(e == null)
//                     return;
//                 await msg.DeleteAsync();
//                 var msg2 = await ctx.Channel.SendConfirmAsync("Please enter your shops URL");
//                 var e2 = await GetUserInputAsync(ctx.User.Id, ctx.Channel.Id);
//                 if(e2 == null)
//                     return;
//                 await msg2.DeleteAsync();
//                 var msg3 = await ctx.Channel.SendConfirmAsync("Please enter the invite link users will use to join for support.");
//                 var e3 = await GetUserInputAsync(ctx.User.Id, ctx.Channel.Id);
//                 if(e3 == null)
//                     return;
//                 await msg3.DeleteAsync();
//                 var msg4 = await ctx.Channel.SendConfirmAsync("Please say the current status of the shop.");
//                 var e4 = await GetUserInputAsync(ctx.User.Id, ctx.Channel.Id);
//                 if(e4 == null)
//                     return;
//                 await msg4.DeleteAsync();
//                 var msg5 = await ctx.Channel.SendConfirmAsync("Please set extra owners here and follow this format: `UserId,UserId,UserId`. The users need to be in the current server.");
//                 var e5 = await GetUserInputAsync(ctx.User.Id, ctx.Channel.Id);
//                 if(e5 == null)
//                     return;
//                 await _service.AddShop(ctx.Guild.Id, ctx.User.Id, e, e2, e3, e4, e5);
//                 await msg5.DeleteAsync();
//                 await ctx.Channel.SendConfirmAsync("Shop add test complete! Stay tuned for the actual shop add request stuffs.");

//             }  
//             [MewdekoCommand]
//             [RequireContext((ContextType.Guild))]
//             public async Task Shops()
//             {
//                 var embed = new EmbedBuilder();
//                 embed.WithOkColor();
//                 var e = _service.GetAll();
//                 foreach(var i in e)
//                 {
//                     var owner = await ctx.Client.GetUserAsync(i.Owner);
//                     if(i.ExtraOwners == null || !i.ExtraOwners.Contains(","))
//                     {
//                         embed.AddField(i.ShopName, $"Current Status: {i.Status}\nShop Url: {i.ShopUrl}\nDiscord Invite: {i.InviteLink}\nMain Owner: {owner.Username}\nLatest Announcement: {i.Announcement}");
//                     }
//                 }
//                 await ctx.Channel.SendMessageAsync(embed: embed.Build());
//             }
//         }
//     }
// }

