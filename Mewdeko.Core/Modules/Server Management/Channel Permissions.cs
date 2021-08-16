using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;

namespace Mewdeko.Modules.ServerManagement
{
    public partial class ServerManagement
    {
        public class ChannelPerms : MewdekoSubmodule
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(1)]
            public async Task DenyPerm(SocketGuildChannel channel, string perm, IRole role)
            {
                var currentPerms = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendMessages: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Read Channel permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(readMessageHistory: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(mentionEveryone: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(addReactions: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useExternalEmojis: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(attachFiles: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(embedLinks: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Embed Links permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendTTSMessages: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(2)]
            public async Task DenyPerm(SocketVoiceChannel channel, string perm, IRole role)
            {
                var currentPerms = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(speak: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(connect: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Connect permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(stream: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Stream permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(muteMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Mute Members permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(deafenMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(moveMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Move Members permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useVoiceActivation: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(prioritySpeaker: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The View Channel permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(1)]
            public async Task AllowPerm(SocketGuildChannel channel, string perm, IRole role)
            {
                var currentPerms = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendMessages: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Read Channel permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(readMessageHistory: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(mentionEveryone: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(addReactions: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useExternalEmojis: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(attachFiles: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(embedLinks: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Embed Links permission has been allowed for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendTTSMessages: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(2)]
            public async Task AllowPerm(SocketVoiceChannel channel, string perm, IRole role)
            {
                var currentPerms = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(speak: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been allowed for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(connect: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Connect permission has been allowed for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(stream: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Stream permission has been allowed for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(muteMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mute Members permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(deafenMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(moveMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Move Members permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useVoiceActivation: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(prioritySpeaker: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Channel permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(3)]
            public async Task AllowPerm(ICategoryChannel channel, string perm, IRole role)
            {
                var currentPerms = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(speak: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been allowed for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(connect: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Connect permission has been allowed for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(stream: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Stream permission has been allowed for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(muteMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mute Members permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(deafenMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(moveMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Move Members permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useVoiceActivation: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(prioritySpeaker: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Channel permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendMessages: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Read Channel permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(readMessageHistory: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(mentionEveryone: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(addReactions: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useExternalEmojis: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(attachFiles: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(embedLinks: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Embed Links permission has been allowed for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendTTSMessages: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been allowed for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(3)]
            public async Task DenyPerm(ICategoryChannel channel, string perm, IRole role)
            {
                var currentPerms = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(speak: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(connect: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Connect permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(stream: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Stream permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(muteMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Mute Members permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(deafenMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(moveMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Move Members permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useVoiceActivation: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(prioritySpeaker: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The View Channel permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendMessages: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Read Channel permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(readMessageHistory: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(mentionEveryone: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(addReactions: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useExternalEmojis: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(attachFiles: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(embedLinks: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Embed Links permission has been denied for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendTTSMessages: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been denied for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(4)]
            public async Task NeutralPerm(ICategoryChannel channel, string perm, IRole role)
            {
                var currentPerms = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(speak: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been set to nuetral for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(connect: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Connect permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(stream: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Stream permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(muteMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mute Members permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(deafenMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(moveMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Move Members permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useVoiceActivation: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(prioritySpeaker: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Channel permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendMessages: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Read Channel permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(readMessageHistory: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(mentionEveryone: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(addReactions: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useExternalEmojis: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(attachFiles: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(embedLinks: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Embed Links permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been set to nuetral for the role " +
                            role.Mention + " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendTTSMessages: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(3)]
            public async Task AllowPerm(ICategoryChannel channel, string perm, IUser User)
            {
                var currentPerms = channel.GetPermissionOverwrite(User) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(speak: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been allowed for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(connect: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Connect permission has been allowed for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(stream: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Stream permission has been allowed for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(muteMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mute Members permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(deafenMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(moveMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Move Members permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(useVoiceActivation: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(prioritySpeaker: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(viewChannel: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Channel permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(sendMessages: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(viewChannel: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Read Channel permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(readMessageHistory: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(mentionEveryone: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(addReactions: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(useExternalEmojis: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(attachFiles: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(embedLinks: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Embed Links permission has been allowed for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(sendTTSMessages: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been allowed for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(3)]
            public async Task DenyPerm(ICategoryChannel channel, string perm, IUser User)
            {
                var currentPerms = channel.GetPermissionOverwrite(User) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(speak: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been denied for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(connect: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Connect permission has been denied for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(stream: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Stream permission has been denied for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(muteMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Mute Members permission has been denied for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(deafenMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(moveMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Move Members permission has been denied for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(useVoiceActivation: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(prioritySpeaker: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(viewChannel: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The View Channel permission has been denied for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(sendMessages: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(viewChannel: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Read Channel permission has been denied for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(readMessageHistory: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(mentionEveryone: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(addReactions: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(useExternalEmojis: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(attachFiles: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(embedLinks: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Embed Links permission has been denied for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(sendTTSMessages: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been denied for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(4)]
            public async Task NeutralPerm(ICategoryChannel channel, string perm, IUser User)
            {
                var currentPerms = channel.GetPermissionOverwrite(User) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(speak: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been set to nuetral for the User " +
                                                           User.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(connect: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Connect permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(stream: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Stream permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(muteMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mute Members permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(deafenMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(moveMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Move Members permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(useVoiceActivation: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(prioritySpeaker: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(viewChannel: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Channel permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(sendMessages: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(viewChannel: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Read Channel permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(readMessageHistory: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(mentionEveryone: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(addReactions: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(useExternalEmojis: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(attachFiles: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(embedLinks: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Embed Links permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(User, currentPerms.Modify(PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been set to nuetral for the User " +
                            User.Mention + " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(User,
                            currentPerms.Modify(sendTTSMessages: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been set to nuetral for the User " + User.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(1)]
            public async Task NeutralPerm(SocketGuildChannel channel, string perm, IRole role)
            {
                var currentPerms = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendMessages: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Read Channel permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(readMessageHistory: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(mentionEveryone: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(addReactions: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useExternalEmojis: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(attachFiles: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(embedLinks: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Embed Links permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been set to nuetral for the role " +
                            role.Mention + " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(sendTTSMessages: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(2)]
            public async Task NeutralPerm(SocketVoiceChannel channel, string perm, IRole role)
            {
                var currentPerms = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(speak: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been set to nuetral for the role " +
                                                           role.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(connect: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Connect permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(role, currentPerms.Modify(stream: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Stream permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(muteMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mute Members permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(deafenMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(moveMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Move Members permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(useVoiceActivation: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(prioritySpeaker: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(role,
                            currentPerms.Modify(viewChannel: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Channel permission has been set to nuetral for the role " + role.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(3)]
            public async Task DenyPerm(SocketGuildChannel channel, string perm, IGuildUser user)
            {
                var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(sendMessages: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(viewChannel: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Read Channel permission has been denied for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(readMessageHistory: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(mentionEveryone: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(addReactions: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(useExternalEmojis: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(attachFiles: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(embedLinks: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Embed Links permission has been denied for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(sendTTSMessages: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(4)]
            public async Task DenyPerm(SocketVoiceChannel channel, string perm, IGuildUser user)
            {
                var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(speak: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been denied for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(connect: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Connect permission has been denied for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(stream: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Stream permission has been denied for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(muteMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Mute Members permission has been denied for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(deafenMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(moveMembers: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The Move Members permission has been denied for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(useVoiceActivation: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(prioritySpeaker: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been denied for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(viewChannel: PermValue.Deny));
                        await ctx.Channel.SendConfirmAsync("The View Channel permission has been denied for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(3)]
            public async Task AllowPerm(SocketGuildChannel channel, string perm, IGuildUser user)
            {
                var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(sendMessages: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(viewChannel: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Read Channel permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(readMessageHistory: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(mentionEveryone: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(addReactions: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(useExternalEmojis: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(attachFiles: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(embedLinks: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Embed Links permission has been allowed for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(sendTTSMessages: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(4)]
            public async Task AllowPerm(SocketVoiceChannel channel, string perm, IGuildUser user)
            {
                var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(speak: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been allowed for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(connect: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Connect permission has been allowed for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(stream: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync("The Stream permission has been allowed for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(muteMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mute Members permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(deafenMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(moveMembers: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Move Members permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(useVoiceActivation: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(prioritySpeaker: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(viewChannel: PermValue.Allow));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Channel permission has been allowed for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(3)]
            public async Task NeutralPerm(SocketGuildChannel channel, string perm, IGuildUser user)
            {
                var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "sendmessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(sendMessages: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send Messages permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "readmessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(viewChannel: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Read Channel permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewhistory":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(readMessageHistory: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Message History permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mentioneveryone":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(mentionEveryone: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mention Everyone permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "addreactions":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(addReactions: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Add Reactions permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "useexternalemojis":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(useExternalEmojis: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use External Emojis permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "managemessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(attachFiles: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Manage Messages permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "embedlinks":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(embedLinks: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Embed Links permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been set to nuetral for the user " +
                            user.Mention + " in the channel " + channel.Name);
                        break;
                    case "sendttsmessages":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(sendTTSMessages: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Send TTS Messages permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(4)]
            public async Task NeutralPerm(SocketVoiceChannel channel, string perm, IGuildUser user)
            {
                var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
                switch (perm)
                {
                    case "speak":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(speak: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync("The Talk permission has been set to nuetral for the user " +
                                                           user.Mention + " in the channel " + channel.Name);
                        break;
                    case "connect":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(connect: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Connect permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "stream":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(stream: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Stream permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "mutemembers":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(muteMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Mute Members permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "deafenmembers":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(deafenMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Deafen Members permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "movemembers":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(moveMembers: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Move Members permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "usevoiceactivation":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(useVoiceActivation: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Use Voice Activation permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "priorityspeaker":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(prioritySpeaker: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Priority Speaker permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "viewchannel":
                        await channel.AddPermissionOverwriteAsync(user,
                            currentPerms.Modify(viewChannel: PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The View Channel permission has been set to nuetral for the user " + user.Mention +
                            " in the channel " + channel.Name);
                        break;
                    case "createinvite":
                        await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(PermValue.Inherit));
                        await ctx.Channel.SendConfirmAsync(
                            "The Create Instant Invite permission has been set to nuetral for the user " +
                            user.Mention + " in the channel " + channel.Name);
                        break;
                }
            }
        }
    }
}