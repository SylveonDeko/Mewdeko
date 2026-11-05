using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class SlashServerTools
{
    /// <summary>
    ///     Slash commands for saving this server's structure as a template and applying templates to a server.
    /// </summary>
    [Group("template", "Save this server's layout as a template and apply templates")]
    public class SlashServerTemplate : MewdekoSlashModuleBase<ServerTemplateService>
    {
        /// <summary>
        ///     Saves this server's roles, categories and channels as a template.
        /// </summary>
        /// <param name="name">The template name.</param>
        [SlashCommand("save", "Save this server's roles and channels as a template")]
        [RequireContext(ContextType.Guild)]
        public async Task Save([Summary("name", "The template name")] string name)
        {
            var guildId = ctx.Guild.Id;
            await DeferAsync(true);
            if (!ServerBackupService.IsOwnerOrAdmin(ctx.Guild, (IGuildUser)ctx.User))
            {
                await EphemeralReplyErrorAsync(Strings.BackupNotAdmin(guildId));
                return;
            }

            var clean = ServerTemplateService.NormalizeName(name);
            if (clean is null)
            {
                await EphemeralReplyErrorAsync(Strings.TemplateBadName(guildId, ServerTemplateService.MaxNameLength));
                return;
            }

            if (ServerTemplateService.IsBuiltIn(clean))
            {
                await EphemeralReplyErrorAsync(Strings.TemplateBuiltIn(guildId, clean));
                return;
            }

            var template = await Service.SaveAsync(ctx.Guild, ctx.User.Id, clean);
            await EphemeralReplyConfirmAsync(Strings.TemplateSaved(guildId, template.Name, template.RoleCount,
                template.ChannelCount));
        }

        /// <summary>
        ///     Lists the starter templates and your saved templates.
        /// </summary>
        [SlashCommand("list", "List the starter templates and your saved templates")]
        [RequireContext(ContextType.Guild)]
        public async Task List()
        {
            var guildId = ctx.Guild.Id;
            var lines = BuiltInServerTemplates.Names.Select(x => Strings.TemplateListBuiltIn(guildId, x)).ToList();
            lines.AddRange((await Service.ListAsync(ctx.User.Id)).Select(x => Strings.TemplateListLine(guildId,
                x.Name, x.RoleCount, x.ChannelCount, Timestamp(x.DateAdded))));
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.TemplateListTitle(guildId))
                .WithDescription(string.Join("\n", lines));
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        /// <summary>
        ///     Deletes one of your saved templates.
        /// </summary>
        /// <param name="name">The template name.</param>
        [SlashCommand("delete", "Delete one of your saved templates")]
        [RequireContext(ContextType.Guild)]
        public async Task Delete([Summary("name", "The template name")] string name)
        {
            var guildId = ctx.Guild.Id;
            var clean = ServerTemplateService.NormalizeName(name) ?? "";
            if (ServerTemplateService.IsBuiltIn(clean))
            {
                await EphemeralReplyErrorAsync(Strings.TemplateBuiltIn(guildId, clean));
                return;
            }

            if (!await Service.DeleteAsync(ctx.User.Id, clean))
            {
                await EphemeralReplyErrorAsync(Strings.TemplateNotFound(guildId, clean));
                return;
            }

            await EphemeralReplyConfirmAsync(Strings.TemplateDeleted(guildId, clean));
        }

        /// <summary>
        ///     Applies a template to this server by adding the roles and channels it is missing.
        /// </summary>
        /// <param name="name">The starter template or one of your saved templates.</param>
        [SlashCommand("apply", "Add the roles and channels a template has that this server is missing")]
        [RequireContext(ContextType.Guild)]
        public async Task Apply([Summary("name", "The starter template or one of your saved templates")] string name)
        {
            var guildId = ctx.Guild.Id;
            await DeferAsync(true);
            var clean = ServerTemplateService.NormalizeName(name) ?? "";
            var data = await Service.GetDataAsync(ctx.User.Id, clean);
            if (data is null)
            {
                await EphemeralReplyErrorAsync(Strings.TemplateNotFound(guildId, clean));
                return;
            }

            var (error, detail) = await Service.CheckApplyAsync(ctx.Guild, (IGuildUser)ctx.User);
            if (error != BackupError.None)
            {
                await EphemeralReplyErrorAsync(Service.ErrorText(guildId, error, detail));
                return;
            }

            if (!await PromptUserConfirmAsync(Strings.TemplateConfirmApply(guildId, clean), ctx.User.Id, true))
            {
                await EphemeralReplyErrorAsync(Strings.BackupCancelled(guildId));
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
