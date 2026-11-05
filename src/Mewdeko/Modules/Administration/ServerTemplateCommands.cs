using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for saving this server's structure as a template and applying templates to a server.
    /// </summary>
    [Group]
    public class ServerTemplateCommands : MewdekoSubmodule<ServerTemplateService>
    {
        /// <summary>
        ///     Saves this server's roles, categories and channels as a template.
        /// </summary>
        /// <param name="name">The template name.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task TemplateSave([Remainder] string name)
        {
            var guildId = ctx.Guild.Id;
            if (!ServerBackupService.IsOwnerOrAdmin(ctx.Guild, (IGuildUser)ctx.User))
            {
                await ReplyErrorAsync(Strings.BackupNotAdmin(guildId));
                return;
            }

            var clean = ServerTemplateService.NormalizeName(name);
            if (clean is null)
            {
                await ReplyErrorAsync(Strings.TemplateBadName(guildId, ServerTemplateService.MaxNameLength));
                return;
            }

            if (ServerTemplateService.IsBuiltIn(clean))
            {
                await ReplyErrorAsync(Strings.TemplateBuiltIn(guildId, clean));
                return;
            }

            var template = await Service.SaveAsync(ctx.Guild, ctx.User.Id, clean);
            await ReplyConfirmAsync(Strings.TemplateSaved(guildId, template.Name, template.RoleCount,
                template.ChannelCount));
        }

        /// <summary>
        ///     Lists the starter templates and your saved templates.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task TemplateList()
        {
            var guildId = ctx.Guild.Id;
            var lines = BuiltInServerTemplates.Names.Select(x => Strings.TemplateListBuiltIn(guildId, x)).ToList();
            lines.AddRange((await Service.ListAsync(ctx.User.Id)).Select(x => Strings.TemplateListLine(guildId,
                x.Name, x.RoleCount, x.ChannelCount, Timestamp(x.DateAdded))));
            await ctx.Channel.SendConfirmAsync(string.Join("\n", lines), Strings.TemplateListTitle(guildId));
        }

        /// <summary>
        ///     Deletes one of your saved templates.
        /// </summary>
        /// <param name="name">The template name.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task TemplateDelete([Remainder] string name)
        {
            var guildId = ctx.Guild.Id;
            var clean = ServerTemplateService.NormalizeName(name) ?? "";
            if (ServerTemplateService.IsBuiltIn(clean))
            {
                await ReplyErrorAsync(Strings.TemplateBuiltIn(guildId, clean));
                return;
            }

            if (!await Service.DeleteAsync(ctx.User.Id, clean))
            {
                await ReplyErrorAsync(Strings.TemplateNotFound(guildId, clean));
                return;
            }

            await ReplyConfirmAsync(Strings.TemplateDeleted(guildId, clean));
        }

        /// <summary>
        ///     Applies a template to this server by adding the roles and channels it is missing.
        /// </summary>
        /// <param name="name">The starter template or one of your saved templates.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task TemplateApply([Remainder] string name)
        {
            var guildId = ctx.Guild.Id;
            var clean = ServerTemplateService.NormalizeName(name) ?? "";
            var data = await Service.GetDataAsync(ctx.User.Id, clean);
            if (data is null)
            {
                await ReplyErrorAsync(Strings.TemplateNotFound(guildId, clean));
                return;
            }

            var (error, detail) = await Service.CheckApplyAsync(ctx.Guild, (IGuildUser)ctx.User);
            if (error != BackupError.None)
            {
                await ReplyErrorAsync(Service.ErrorText(guildId, error, detail));
                return;
            }

            if (!await PromptUserConfirmAsync(Strings.TemplateConfirmApply(guildId, clean), ctx.User.Id))
            {
                await ReplyErrorAsync(Strings.BackupCancelled(guildId));
                return;
            }

            await Service.ApplyAsync(ctx.Guild, ctx.User, ctx.Channel, data);
        }

        private static string Timestamp(DateTime utc)
        {
            return $"<t:{new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds()}:R>";
        }
    }
}
