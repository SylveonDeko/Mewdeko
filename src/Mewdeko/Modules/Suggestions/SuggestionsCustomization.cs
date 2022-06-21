using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Suggestions.Services;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Suggestions;

public partial class Suggestions
{
    public enum SuggestThreadType
    {
        None = 0,
        Public = 1,
        Private = 2
    }

    public enum SuggestEmoteModeEnum
    {
        Reactions = 0,
        Buttons = 1,
    }

    public enum ButtonType
    {
        Blurple = 1,
        Blue = 1,
        Grey = 2,
        Gray = 2,
        Green = 3,
        Red = 4
    }

    public enum RepostThreshold
    {
        Disabled = 0,
        Five = 5,
        Ten = 10,
        Fifteen = 15
    }
    [Group]
    public class SuggestionsCustomization : MewdekoModuleBase<SuggestionsService>
    {
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task SuggestMessage([Remainder] string embed)
        {
            if (embed == "-")
            {
                await Service.SetSuggestionMessage(ctx.Guild, embed);
                await ctx.Channel.SendConfirmAsync("Suggestions will now have the default look.");
                return;
            }

            await Service.SetSuggestionMessage(ctx.Guild, embed);
            await ctx.Channel.SendConfirmAsync("Sucessfully updated suggestion message!");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task MinSuggestionLength(int length)
        {
            if (length >= 2048)
            {
                await ctx.Channel.SendErrorAsync("Can't set this value because it means users will not be able to suggest anything!");
                return;
            }

            await Service.SetMinLength(ctx.Guild, length);
            await ctx.Channel.SendConfirmAsync($"Minimum length set to {length} characters!");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task SuggestMotesMode(SuggestEmoteModeEnum mode)
        {
            await Service.SetEmoteMode(ctx.Guild, (int)mode);
            await ctx.Channel.SendConfirmAsync($"Sucessfully set Emote Mode to {mode}");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task SuggestButtonColor(ButtonType type)
        {
            await Service.SetSuggestButtonColor(ctx.Guild, (int)type);
            await ctx.Channel.SendConfirmAsync($"Suggest Button Color will now be `{type}`");
            await Service.UpdateSuggestionButtonMessage(ctx.Guild, Service.GetSuggestButtonMessage(ctx.Guild));
        }
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task SuggestMoteColor(int num, ButtonType type)
        {
            await Service.SetButtonType(ctx.Guild, num, (int)type);
            await ctx.Channel.SendConfirmAsync($"Suggest Button {num} will now be `{type}`");
            await Service.UpdateSuggestionButtonMessage(ctx.Guild, Service.GetSuggestButtonMessage(ctx.Guild));
        }
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task MaxSuggestionLength(int length)
        {
            if (length <= 0)
            {
                await ctx.Channel.SendErrorAsync("Cant set this value because it means users will not be able to suggest anything!");
                return;
            }

            await Service.SetMaxLength(ctx.Guild, length);
            await ctx.Channel.SendConfirmAsync($"Max length set to {length} characters!");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task AcceptMessage([Remainder] string embed)
        {
            if (embed == "-")
            {
                await Service.SetAcceptMessage(ctx.Guild, embed);
                await ctx.Channel.SendConfirmAsync("Accpeted Suggestions will now have the default look.");
                return;
            }

            await Service.SetAcceptMessage(ctx.Guild, embed);
            await ctx.Channel.SendConfirmAsync("Sucessfully updated accpeted suggestion message!");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task AcceptChannel(ITextChannel? channel = null)
        {
            await Service.SetAcceptChannel(ctx.Guild, channel?.Id ?? 0);
            if (channel is null)
                await ctx.Channel.SendConfirmAsync("Accept Channel Disabled.");
            else
                await ctx.Channel.SendConfirmAsync($"Accept channel set to {channel.Mention}");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task DenyChannel(ITextChannel? channel = null)
        {
            await Service.SetDenyChannel(ctx.Guild, channel?.Id ?? 0);
            if (channel is null)
                await ctx.Channel.SendConfirmAsync("Deny Channel Disabled.");
            else
                await ctx.Channel.SendConfirmAsync($"Deny channel set to {channel.Mention}");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task ConsiderChannel(ITextChannel? channel = null)
        {
            await Service.SetConsiderChannel(ctx.Guild, channel?.Id ?? 0);
            if (channel is null)
                await ctx.Channel.SendConfirmAsync("Consider Channel Disabled.");
            else
                await ctx.Channel.SendConfirmAsync($"Consider channel set to {channel.Mention}");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task ImplementChannel(ITextChannel? channel = null)
        {
            await Service.SetImplementChannel(ctx.Guild, channel?.Id ?? 0);
            if (channel is null)
                await ctx.Channel.SendConfirmAsync("Implement Channel Disabled.");
            else
                await ctx.Channel.SendConfirmAsync($"Implement channel set to {channel.Mention}");
        }
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task ImplementMessage([Remainder] string embed)
        {
            if (embed == "-")
            {
                await Service.SetImplementMessage(ctx.Guild, embed);
                await ctx.Channel.SendConfirmAsync("Implemented Suggestions will now have the default look.");
                return;
            }

            await Service.SetImplementMessage(ctx.Guild, embed);
            await ctx.Channel.SendConfirmAsync("Sucessfully updated implemented suggestion message!");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task SuggestThreadsType(SuggestThreadType type)
        {
            if (type == SuggestThreadType.Private && !ctx.Guild.Features.HasPrivateThreads)
            {
                await ctx.Channel.SendErrorAsync("You do not have enough server boosts for private threads!");
                return;
            }
            await Service.SetSuggestThreadsType(ctx.Guild, (int)type);
            await ctx.Channel.SendConfirmAsync($"Succesfully set Suggestion Threads Type to `{type}`");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task SuggestButtonChannel(ITextChannel channel)
        {
            await Service.SetSuggestButtonChannel(ctx.Guild, channel.Id);
            await Service.UpdateSuggestionButtonMessage(ctx.Guild, Service.GetSuggestButtonMessage(ctx.Guild), true);
            await ctx.Channel.SendConfirmAsync($"Suggest Button Channel set to {channel.Mention}");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task SuggestButtonMessage([Remainder] string? toSet)
        {
            if (toSet == "-")
            {
                await ctx.Channel.SendConfirmAsync("Succesfully set suggest button message back to default.");
                await Service.SetSuggestButtonMessage(ctx.Guild, "-");
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, toSet);
            }
            else
            {
                await ctx.Channel.SendConfirmAsync("Succesfully set suggest button message to a custom message.");
                await Service.SetSuggestButtonMessage(ctx.Guild, toSet);
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, toSet);
            }
        }
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task SuggestButtonLabel([Remainder] string toSet)
        {
            if (toSet.Length > 80)
            {
                await ctx.Channel.SendErrorAsync("The max length for labels is 80 characters!");
                return;
            }
            if (toSet is "-" or "disabled")
            {
                await ctx.Channel.SendConfirmAsync("Succesfully set suggest button label back to default.");
                await Service.SetSuggestButtonLabel(ctx.Guild, "-");
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, Service.GetSuggestButtonMessage(ctx.Guild));
            }
            else
            {
                await ctx.Channel.SendConfirmAsync($"Succesfully set suggest button label to `{toSet}`.");
                await Service.SetSuggestButtonLabel(ctx.Guild, toSet);
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, Service.GetSuggestButtonMessage(ctx.Guild));
            }
        }
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task SuggestButtonEmote(IEmote? emote = null)
        {
            if (emote is null)
            {
                await ctx.Channel.SendConfirmAsync("Succesfully set suggest button emote back to default.");
                await Service.SetSuggestButtonEmote(ctx.Guild, "-");
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, Service.GetSuggestButtonMessage(ctx.Guild));
            }
            else
            {
                await ctx.Channel.SendConfirmAsync($"Succesfully set suggest button label to `{emote}`");
                await Service.SetSuggestButtonEmote(ctx.Guild, emote.ToString());
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, Service.GetSuggestButtonMessage(ctx.Guild));
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task ArchiveOnDeny()
        {
            var current = Service.GetArchiveOnDeny(ctx.Guild);
            await Service.SetArchiveOnDeny(ctx.Guild, !current);
            await ctx.Channel.SendConfirmAsync($"Archive on deny is now set to `{!current}`");
        }
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task ArchiveOnAccept()
        {
            var current = Service.GetArchiveOnAccept(ctx.Guild);
            await Service.SetArchiveOnAccept(ctx.Guild, !current);
            await ctx.Channel.SendConfirmAsync($"Archive on accept is now set to `{!current}`");
        }
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task ArchiveOnConsider()
        {
            var current = Service.GetArchiveOnConsider(ctx.Guild);
            await Service.SetArchiveOnConsider(ctx.Guild, !current);
            await ctx.Channel.SendConfirmAsync($"Archive on consider is now set to `{!current}`");
        }
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task ArchiveOnImplement()
        {
            var current = Service.GetArchiveOnImplement(ctx.Guild);
            await Service.SetArchiveOnImplement(ctx.Guild, !current);
            await ctx.Channel.SendConfirmAsync($"Archive on implement is now set to `{!current}`");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task DenyMessage([Remainder] string embed)
        {
            if (embed == "-")
            {
                await Service.SetDenyMessage(ctx.Guild, embed);
                await ctx.Channel.SendConfirmAsync("Denied Suggestions will now have the default look.");
                return;
            }

            await Service.SetDenyMessage(ctx.Guild, embed);
            await ctx.Channel.SendConfirmAsync("Sucessfully updated denied suggestion message!");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task ConsiderMessage([Remainder] string embed)
        {
            if (embed == "-")
            {
                await Service.SetConsiderMessage(ctx.Guild, embed);
                await ctx.Channel.SendConfirmAsync("Suggestions will now have the default look.");
                return;
            }

            await Service.SetConsiderMessage(ctx.Guild, embed);
            await ctx.Channel.SendConfirmAsync("Sucessfully updated suggestion message!");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task SuggestMotes([Remainder] string? _ = null)
        {
            if (_ == null)
            {
                await ctx.Channel.SendErrorAsync("You need to either provide emojis or say disable for this to work!");
                return;
            }

            if (_ != null && _.Contains("disable"))
            {
                await Service.SetSuggestionEmotes(ctx.Guild, "disable");
                await ctx.Channel.SendConfirmAsync("Disabled Custom Emotes for Suggestions");
                return;
            }

            if (_ != null && !_.Contains("disable") && ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value).Count() > 5)
            {
                await ctx.Channel.SendErrorAsync("You may only have up to 5 emotes for suggestions!");
                return;
            }

            if (!_.Contains("disable") && !ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (IEmote)x.Value).Any())
            {
                await ctx.Channel.SendErrorAsync("You need to specify up to 5 emotes for this command to work!");
                return;
            }

            var emotes = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (IEmote)x.Value);
            foreach (var emoji in emotes)
            {
                try
                {
                    await ctx.Message.AddReactionAsync(emoji);
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync($"Unable to access the emote {emoji.Name}, please add me to the server it's in or use a different emote.");
                    return;
                }
            }

            var list = emotes.Select(emote => emote.ToString()).ToList();
            await Service.SetSuggestionEmotes(ctx.Guild, string.Join(",", list));
            await ctx.Channel.SendConfirmAsync($"Suggestions will now be reacted with {string.Join(",", list)}");
        }
    }
}