using Discord.Interactions;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Utility.Services;
using Embed = DataModel.Embed;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Provides slash commands for managing and using embed templates within a guild and per-user.
/// </summary>
[Group("embed", "Save, list, preview and delete embed templates")]
public class SlashEmbed(IDataConnectionFactory dbFactory) : MewdekoSlashModuleBase<EmbedService>
{
    /// <summary>
    ///     Opens a modal to save an embed template for personal use.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("save", "Saves a personal embed template")]
    [CheckPermissions]
    public Task EmbedSave()
    {
        return RespondWithModalAsync<EmbedSaveModal>("embed_save:personal");
    }

    /// <summary>
    ///     Opens a modal to save an embed template for guild use (requires ManageMessages permission).
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("guild-save", "Saves an embed template shared with this server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public Task GuildEmbedSave()
    {
        return RespondWithModalAsync<EmbedSaveModal>("embed_save:guild");
    }

    /// <summary>
    ///     Handles the embed save modal and stores the template either for personal or guild use.
    /// </summary>
    /// <param name="kind">Either personal or guild, indicating where the template is stored.</param>
    /// <param name="modal">The modal containing the template name and json.</param>
    /// <returns>A task that represents the asynchronous operation of saving an embed template.</returns>
    [ModalInteraction("embed_save:*", true)]
    public async Task EmbedSaveSubmitted(string kind, EmbedSaveModal modal)
    {
        var name = modal.Name;
        var embedJson = modal.Json;
        var isGuild = kind == "guild";
        var guildId = ctx.Guild?.Id ?? 0;

        if (isGuild && ctx.Guild is null)
            return;

        if (string.IsNullOrWhiteSpace(name))
        {
            await ReplyErrorAsync(Strings.EmbedSaveNameRequired(guildId)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(embedJson))
        {
            await ReplyErrorAsync(Strings.EmbedSaveJsonRequired(guildId)).ConfigureAwait(false);
            return;
        }

        if (!SmartEmbed.TryParse(embedJson, ctx.Guild?.Id, out var embedData, out var plainText,
                out var components))
        {
            await ReplyErrorAsync(Strings.EmbedSaveInvalidJson(guildId)).ConfigureAwait(false);
            return;
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        if (isGuild)
        {
            var existingGuildEmbed = await db.GetTable<Embed>()
                .FirstOrDefaultAsync(e => e.GuildId == ctx.Guild.Id &&
                                          e.EmbedName == name &&
                                          e.IsGuildShared == true);

            if (existingGuildEmbed != null)
            {
                await ReplyErrorAsync(Strings.EmbedSaveAlreadyExists(guildId, name)).ConfigureAwait(false);
                return;
            }

            var guildEmbed = new Embed
            {
                UserId = ctx.User.Id,
                EmbedName = name,
                JsonCode = embedJson,
                DateAdded = DateTime.UtcNow,
                GuildId = ctx.Guild.Id,
                IsGuildShared = true
            };

            await db.InsertAsync(guildEmbed);

            await ReplyConfirmAsync(Strings.GuildEmbedSaveSuccess(guildId, name)).ConfigureAwait(false);
            return;
        }

        var existingEmbed = await db.GetTable<Embed>()
            .FirstOrDefaultAsync(e => e.UserId == ctx.User.Id &&
                                      e.EmbedName == name &&
                                      e.GuildId == null);

        if (existingEmbed != null)
        {
            await ReplyErrorAsync(Strings.EmbedSaveAlreadyExists(guildId, name)).ConfigureAwait(false);
            return;
        }

        var embed = new Embed
        {
            UserId = ctx.User.Id,
            EmbedName = name,
            JsonCode = embedJson,
            DateAdded = DateTime.UtcNow,
            GuildId = null,
            IsGuildShared = false
        };

        await db.InsertAsync(embed);

        await ReplyConfirmAsync(Strings.EmbedSaveSuccess(guildId, name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all personal embed templates.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of listing embed templates.</returns>
    [SlashCommand("list", "Lists your personal embed templates")]
    [CheckPermissions]
    public async Task EmbedList()
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var userEmbeds = await db.GetTable<Embed>()
            .Where(e => e.UserId == ctx.User.Id && e.GuildId == null)
            .OrderBy(e => e.EmbedName)
            .ToListAsync();

        if (!userEmbeds.Any())
        {
            await ReplyErrorAsync(Strings.EmbedListNone(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
            return;
        }

        var embedNames = userEmbeds.Select(e => $"- {e.EmbedName}").ToList();
        await ReplyConfirmAsync(Strings.EmbedListPersonal(ctx.Guild?.Id ?? 0, string.Join("\n", embedNames)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all guild embed templates.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of listing guild embed templates.</returns>
    [SlashCommand("guild-list", "Lists the embed templates shared with this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task GuildEmbedList()
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var guildEmbeds = await db.GetTable<Embed>()
            .Where(e => e.GuildId == ctx.Guild.Id && e.IsGuildShared == true)
            .OrderBy(e => e.EmbedName)
            .ToListAsync();

        if (!guildEmbeds.Any())
        {
            await ReplyErrorAsync(Strings.GuildEmbedListNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embedNames = guildEmbeds.Select(e => $"- {e.EmbedName}").ToList();
        await ReplyConfirmAsync(Strings.EmbedListGuild(ctx.Guild.Id, string.Join("\n", embedNames)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a personal embed template.
    /// </summary>
    /// <param name="name">The name of the embed template to delete.</param>
    /// <returns>A task that represents the asynchronous operation of deleting an embed template.</returns>
    [SlashCommand("delete", "Deletes one of your personal embed templates")]
    [CheckPermissions]
    public async Task EmbedDelete([Summary("name", "The name of the template")] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            await ReplyErrorAsync(Strings.EmbedDeleteNameRequired(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
            return;
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        var embed = await db.GetTable<Embed>()
            .FirstOrDefaultAsync(e => e.UserId == ctx.User.Id &&
                                      e.EmbedName == name &&
                                      e.GuildId == null);

        if (embed == null)
        {
            await ReplyErrorAsync(Strings.EmbedDeleteNotFound(ctx.Guild?.Id ?? 0, name)).ConfigureAwait(false);
            return;
        }

        await db.DeleteAsync(embed);

        await ReplyConfirmAsync(Strings.EmbedDeleteSuccess(ctx.Guild?.Id ?? 0, name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a guild embed template (requires ManageMessages permission).
    /// </summary>
    /// <param name="name">The name of the embed template to delete.</param>
    /// <returns>A task that represents the asynchronous operation of deleting a guild embed template.</returns>
    [SlashCommand("guild-delete", "Deletes an embed template shared with this server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task GuildEmbedDelete([Summary("name", "The name of the template")] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            await ReplyErrorAsync(Strings.EmbedDeleteNameRequired(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        var embed = await db.GetTable<Embed>()
            .FirstOrDefaultAsync(e => e.GuildId == ctx.Guild.Id &&
                                      e.EmbedName == name &&
                                      e.IsGuildShared == true);

        if (embed == null)
        {
            await ReplyErrorAsync(Strings.EmbedDeleteNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
            return;
        }

        await db.DeleteAsync(embed);

        await ReplyConfirmAsync(Strings.GuildEmbedDeleteSuccess(ctx.Guild.Id, name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Previews an embed template. Personal templates are checked first, then guild templates.
    /// </summary>
    /// <param name="name">The name of the embed template to preview.</param>
    /// <returns>A task that represents the asynchronous operation of previewing an embed template.</returns>
    [SlashCommand("preview", "Previews an embed template")]
    [CheckPermissions]
    public async Task EmbedPreview([Summary("name", "The name of the template")] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            await ReplyErrorAsync(Strings.EmbedPreviewNameRequired(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
            return;
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        var embed = await db.GetTable<Embed>()
            .FirstOrDefaultAsync(e => e.UserId == ctx.User.Id &&
                                      e.EmbedName == name &&
                                      e.GuildId == null);

        if (embed == null && ctx.Guild != null)
        {
            embed = await db.GetTable<Embed>()
                .FirstOrDefaultAsync(e => e.GuildId == ctx.Guild.Id &&
                                          e.EmbedName == name &&
                                          e.IsGuildShared == true);
        }

        if (embed == null)
        {
            await ReplyErrorAsync(Strings.EmbedPreviewNotFound(ctx.Guild?.Id ?? 0, name)).ConfigureAwait(false);
            return;
        }

        var replacer = new ReplacementBuilder()
            .WithDefault(Context)
            .Build();

        var content = replacer.Replace(embed.JsonCode);

        if (SmartEmbed.TryParse(content, ctx.Guild?.Id, out var embedData, out var plainText, out var components))
        {
            await ctx.Interaction.RespondAsync(plainText ?? "", embedData, components: components?.Build())
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.EmbedPreviewInvalidJson(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
        }
    }
}