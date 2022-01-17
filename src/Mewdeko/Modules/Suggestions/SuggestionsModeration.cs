// using Discord;
// using Discord.Commands;
// using LinqToDB.Reflection;
// using Mewdeko._Extensions;
// using Mewdeko.Common;
// using Mewdeko.Common.Attributes;
// using Mewdeko.Modules.Permissions.Services;
// using Mewdeko.Modules.Suggestions.Services;
// using Mewdeko.Services.Database.Models;
//
// namespace Mewdeko.Modules.Suggestions;
//
// [Group]
// public class SuggestionsModeration : MewdekoSubmodule<SuggestionsService>
// {
//     private PermissionService permissionService;
//
//     public SuggestionsModeration(PermissionService permissionService) => this.permissionService = permissionService;
//
//     // [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages),
//     //  RequireBotPermission(GuildPermission.ManageMessages)]
//     // public async Task DeleteSuggestions(IUser user)
//     // {
//     //     if (Service.GetSuggestionChannel(ctx.Guild.Id) is 0)
//     //     {
//     //         await ctx.Channel.SendErrorAsync("You do not have a suggestion channel set so this will not work!");
//     //         return;
//     //     }
//     //
//     //     var channel = await ctx.Guild.GetTextChannelAsync(Service.GetSuggestionChannel(ctx.Guild.Id));
//     //     if (channel is null)
//     //     {
//     //         await ctx.Channel.SendErrorAsync(
//     //             "Seems like the set suggestions channel is invalid, so this will not work!");
//     //         return;
//     //     }
//     //
//     //     var suggests = Service.ForUser(ctx.Guild.Id, user.Id);
//     //     if (!suggests.Any())
//     //     {
//     //         await ctx.Channel.SendErrorAsync("This user has no suggestions!");
//     //         return;
//     //     }
//     //
//     //     if (await PromptUserConfirmAsync($"Are you sure you want to delete {suggests.Length} suggestions from {user}?",
//     //             ctx.User.Id))
//     //     {
//     //         await Service.DeleteUserSuggestions(suggests, channel);
//     //         await ctx.Channel.SendConfirmAsync(
//     //             "Suggestions for this user have been removed. If there are any leftover suggestion messages from unsuccessful deletion please delete them manually.");
//     //     }
//     //     
//     // }
//     [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages),
//      RequireBotPermission(GuildPermission.ManageMessages)]
//     public async Task SuggestBlacklist(IUser user)
//     {
//         await permissionService.AddPermissions(ctx.Guild.Id, new Permissionv2
//         {
//             PrimaryTarget = PrimaryPermissionType.User,
//             PrimaryTargetId = user.Id,
//             SecondaryTarget = SecondaryPermissionType.Command,
//             SecondaryTargetName = "suggest",
//             State = false
//         }).ConfigureAwait(false);
//         await ctx.Channel.SendConfirmAsync($"Blacklisted {user.Mention} from suggestions.");
//     }
//     
//     [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages),
//      RequireBotPermission(GuildPermission.ManageMessages)]
//     public async Task SuggestBlacklist(IRole role)
//     {
//         await permissionService.AddPermissions(ctx.Guild.Id, new Permissionv2
//         {
//             PrimaryTarget = PrimaryPermissionType.Role,
//             PrimaryTargetId = role.Id,
//             SecondaryTarget = SecondaryPermissionType.Command,
//             SecondaryTargetName = "suggest",
//             State = false
//         }).ConfigureAwait(false);
//         await ctx.Channel.SendConfirmAsync($"Blacklisted {role.Mention} from suggestions.");
//     }
//     
//     [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages),
//      RequireBotPermission(GuildPermission.ManageMessages)]
//     public async Task SuggestBlacklist(ITextChannel chan)
//     {
//         await permissionService.AddPermissions(ctx.Guild.Id, new Permissionv2
//         {
//             PrimaryTarget = PrimaryPermissionType.Channel,
//             PrimaryTargetId = chan.Id,
//             SecondaryTarget = SecondaryPermissionType.Command,
//             SecondaryTargetName = "suggest",
//             State = false
//         }).ConfigureAwait(false);
//         await ctx.Channel.SendConfirmAsync($"Blacklisted {chan.Mention} from suggestions.");
//     }
//     [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages),
//      RequireBotPermission(GuildPermission.ManageMessages)]
//     public async Task SuggestUnblacklist(IUser user)
//     {
//         await permissionService.
//         await ctx.Channel.SendConfirmAsync($"Blacklisted {user.Mention} from suggestions.");
//     }
//     
//     [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages),
//      RequireBotPermission(GuildPermission.ManageMessages)]
//     public async Task SuggestBlacklist(IRole role)
//     {
//         await permissionService.AddPermissions(ctx.Guild.Id, new Permissionv2
//         {
//             PrimaryTarget = PrimaryPermissionType.Role,
//             PrimaryTargetId = role.Id,
//             SecondaryTarget = SecondaryPermissionType.Command,
//             SecondaryTargetName = "suggest",
//             State = false
//         }).ConfigureAwait(false);
//         await ctx.Channel.SendConfirmAsync($"Blacklisted {role.Mention} from suggestions.");
//     }
//     
//     [MewdekoCommand, Usage, Description, Aliases, RequireUserPermission(GuildPermission.ManageMessages),
//      RequireBotPermission(GuildPermission.ManageMessages)]
//     public async Task SuggestBlacklist(ITextChannel chan)
//     {
//         await permissionService.AddPermissions(ctx.Guild.Id, new Permissionv2
//         {
//             PrimaryTarget = PrimaryPermissionType.Channel,
//             PrimaryTargetId = chan.Id,
//             SecondaryTarget = SecondaryPermissionType.Command,
//             SecondaryTargetName = "suggest",
//             State = false
//         }).ConfigureAwait(false);
//         await ctx.Channel.SendConfirmAsync($"Blacklisted {chan.Mention} from suggestions.");
//     }
// }