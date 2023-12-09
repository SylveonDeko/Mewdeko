#undef FORCE_ADD_DUMMY_PERMS

using Discord.Commands;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using ContextType = Discord.Interactions.ContextType;
using TextUserPermAttribute = Mewdeko.Common.Attributes.TextCommands.UserPermAttribute;

namespace Mewdeko.Modules.Permissions;

[Discord.Interactions.Group("permissions", "Change or view command permissions.")]
public class SlashPermissions : MewdekoSlashModuleBase<PermissionService>
{
    private readonly GuildSettingsService guildSettings;

    public enum PermissionSlash
    {
        Allow = 1,
        Deny = 0
    }

    public enum Reset
    {
        Reset
    }

    private readonly DbService db;
    private readonly InteractiveService interactivity;
    private readonly DiscordPermOverrideService dpoS;
    private readonly CommandService cmdServe;

    public SlashPermissions(DbService db, InteractiveService inter, GuildSettingsService guildSettings,
        DiscordPermOverrideService dpoS, CommandService cmdServe)
    {
        interactivity = inter;
        this.guildSettings = guildSettings;
        this.db = db;
        this.dpoS = dpoS;
        this.cmdServe = cmdServe;
    }

    [SlashCommand("resetperms", "Reset Command Permissions"), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator)]
    public async Task ResetPerms()
    {
        await Service.Reset(ctx.Guild.Id).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("perms_reset").ConfigureAwait(false);
    }

    [SlashCommand("verbose", "Enables or Disables command errors"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task Verbose(PermissionSlash? action = null)
    {
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var config = await uow.GcWithPermissionsv2For(ctx.Guild.Id);
            config.VerbosePermissions = Convert.ToBoolean(action) ? 1 : 0;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);
        }

        if (action == PermissionSlash.Allow)
            await ReplyConfirmLocalizedAsync("verbose_true").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("verbose_false").ConfigureAwait(false);
    }

    [SlashCommand("permrole", "Sets a role to change command permissions without admin"),
     Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), Priority(0)]
    public async Task PermRole(IRole? role = null)
    {
        if (role != null && role == role.Guild.EveryoneRole)
            return;
        await using var uow = db.GetDbContext();

        if (role == null)
        {
            var config = await uow.GcWithPermissionsv2For(ctx.Guild.Id);
            config.PermissionRole = 0.ToString();
            await uow.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);
            await ReplyConfirmLocalizedAsync("permrole_reset").ConfigureAwait(false);
        }

        await using (uow.ConfigureAwait(false))
        {
            var config = await uow.GcWithPermissionsv2For(ctx.Guild.Id);
            config.PermissionRole = role.Id.ToString();
            await uow.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);
        }

        await ReplyConfirmLocalizedAsync("permrole_changed", Format.Bold(role.Name)).ConfigureAwait(false);
    }

    [SlashCommand("listperms", "List currently set permissions"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task ListPerms()
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(perms.Count / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();
        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithDescription(string.Join("\n",
                perms.Skip(page * 10).Take(10).Select(p =>
                {
                    var str =
                        $"`{p.Index + 1}.` {Format.Bold(p.GetCommand(guildSettings.GetPrefix(ctx.Guild).GetAwaiter().GetResult(), (SocketGuild)ctx.Guild))}";
                    if (p.Index == 0)
                        str += $" [{GetText("uneditable")}]";
                    return str;
                }))).WithTitle(Format.Bold(GetText("page", page + 1))).WithOkColor();
        }
    }

    [SlashCommand("removeperm", "Remove a permission based on its number"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task RemovePerm(
        [Discord.Interactions.Summary("permission", "the permission to modify"),
         Autocomplete(typeof(PermissionAutoCompleter))]
        string perm)
    {
        var index = int.Parse(perm);
        if (index == 0)
        {
            await ctx.Interaction.SendErrorAsync("You cannot remove this permission!").ConfigureAwait(false);
            return;
        }

        try
        {
            Permissionv2 p;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.GcWithPermissionsv2For(ctx.Guild.Id);
                var permsCol = new PermissionsCollection<Permissionv2>(config.Permissions);
                p = permsCol[index];
                permsCol.RemoveAt(index);
                uow.Remove(p);
                await uow.SaveChangesAsync().ConfigureAwait(false);
                Service.UpdateCache(config);
            }

            await ReplyConfirmLocalizedAsync("removed",
                    index + 1,
                    Format.Code(p.GetCommand(await guildSettings.GetPrefix(ctx.Guild), (SocketGuild)ctx.Guild)))
                .ConfigureAwait(false);
        }
        catch (IndexOutOfRangeException)
        {
            await ReplyErrorLocalizedAsync("perm_out_of_range").ConfigureAwait(false);
        }
    }

    [SlashCommand("servercommand", "Enable or disable a command in the server"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task ServerCmd(
        [Discord.Interactions.Summary("command", "the command to set permissions on"),
         Autocomplete(typeof(GenericCommandAutocompleter))]
        string command,
        PermissionSlash action)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action) ? 1 : 0,
            IsCustomCommand = 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("sx_enable",
                Format.Code(command),
                GetText("of_command")).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("sx_disable",
                Format.Code(command),
                GetText("of_command")).ConfigureAwait(false);
        }
    }

    [SlashCommand("servermodule", "Enable or disable a Module in the server"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task SrvrMdl(
        [Discord.Interactions.Summary("module", "the module to set permissions on"),
         Autocomplete(typeof(ModuleAutoCompleter))]
        string module,
        PermissionSlash action)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action) ? 1 : 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("sx_enable",
                Format.Code(module),
                GetText("of_module")).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("sx_disable",
                Format.Code(module),
                GetText("of_module")).ConfigureAwait(false);
        }
    }

    [SlashCommand("usercommand", "Enable or disable a command for a user"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task UsrCmd(
        [Discord.Interactions.Summary("command", "the command to set permissions on"),
         Autocomplete(typeof(GenericCommandAutocompleter))]
        string command,
        PermissionSlash action, IGuildUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action) ? 1 : 0,
            IsCustomCommand = 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("ux_enable",
                Format.Code(command),
                GetText("of_command"),
                Format.Code(user.ToString())).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("ux_disable",
                Format.Code(command),
                GetText("of_command"),
                Format.Code(user.ToString())).ConfigureAwait(false);
        }
    }

    [SlashCommand("usermodule", "Enable or disable a module for a user"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task UsrMdl(
        [Discord.Interactions.Summary("module", "the module to set permissions on"),
         Autocomplete(typeof(ModuleAutoCompleter))]
        string module,
        PermissionSlash action, IGuildUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action) ? 1 : 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("ux_enable",
                Format.Code(module),
                GetText("of_module"),
                Format.Code(user.ToString())).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("ux_disable",
                Format.Code(module),
                GetText("of_module"),
                Format.Code(user.ToString())).ConfigureAwait(false);
        }
    }

    [SlashCommand("rolecommand", "Enable or disable a command for a role"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task RoleCmd(
        [Discord.Interactions.Summary("command", "the command to set permissions on"),
         Autocomplete(typeof(GenericCommandAutocompleter))]
        string command,
        PermissionSlash action, IRole role)
    {
        if (role == role.Guild.EveryoneRole)
            return;

        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Role,
            PrimaryTargetId = role.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action) ? 1 : 0,
            IsCustomCommand = 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("rx_enable",
                Format.Code(command),
                GetText("of_command"),
                Format.Code(role.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("rx_disable",
                Format.Code(command),
                GetText("of_command"),
                Format.Code(role.Name)).ConfigureAwait(false);
        }
    }

    [SlashCommand("rolemodule", "Enable or disable a module for a role"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task RoleMdl(
        [Discord.Interactions.Summary("module", "the module to set permissions on"),
         Autocomplete(typeof(ModuleAutoCompleter))]
        string module,
        PermissionSlash action, IRole role)
    {
        if (role == role.Guild.EveryoneRole)
            return;

        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Role,
            PrimaryTargetId = role.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action) ? 1 : 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("rx_enable",
                Format.Code(module),
                GetText("of_module"),
                Format.Code(role.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("rx_disable",
                Format.Code(module),
                GetText("of_module"),
                Format.Code(role.Name)).ConfigureAwait(false);
        }
    }

    [SlashCommand("channelcommand", "Enable or disable a command for a channel"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task ChnlCmd(
        [Discord.Interactions.Summary("command", "the command to set permissions on"),
         Autocomplete(typeof(GenericCommandAutocompleter))]
        string command,
        PermissionSlash action, ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action) ? 1 : 0,
            IsCustomCommand = 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("cx_enable",
                Format.Code(command),
                GetText("of_command"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("cx_disable",
                Format.Code(command),
                GetText("of_command"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
    }

    [SlashCommand("channelmodule", "Enable or disable a module for a channel"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task ChnlMdl(
        [Discord.Interactions.Summary("module", "the module to set permissions on"),
         Autocomplete(typeof(ModuleAutoCompleter))]
        string module,
        PermissionSlash action, ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action) ? 1 : 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("cx_enable",
                Format.Code(module),
                GetText("of_module"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("cx_disable",
                Format.Code(module),
                GetText("of_module"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
    }

    [SlashCommand("allchannelmodules", "Enable or disable all modules in a channel"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task AllChnlMdls(PermissionSlash action, ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = Convert.ToBoolean((int)action) ? 1 : 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("acm_enable",
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("acm_disable",
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
    }

    [SlashCommand("categorycommand", "Enable or disable commands for a category"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task CatCmd(
        [Discord.Interactions.Summary("command", "the command to set permissions on"),
         Autocomplete(typeof(GenericCommandAutocompleter))]
        string command,
        PermissionSlash action, ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action) ? 1 : 0,
            IsCustomCommand = 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("cx_enable",
                Format.Code(command),
                GetText("of_command"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("cx_disable",
                Format.Code(command),
                GetText("of_command"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
    }

    [SlashCommand("categorymodule", "Enable or disable a module for a category"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task CatMdl(
        [Discord.Interactions.Summary("module", "the module to set permissions on"),
         Autocomplete(typeof(ModuleAutoCompleter))]
        string module,
        PermissionSlash action, ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action) ? 1 : 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("cx_enable",
                Format.Code(module),
                GetText("of_module"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("cx_disable",
                Format.Code(module),
                GetText("of_module"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
    }

    [SlashCommand("allcategorymodules", "Enable or disable all modules in a category"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task AllCatMdls(PermissionSlash action, ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = Convert.ToBoolean((int)action) ? 1 : 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("acm_enable",
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("acm_disable",
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
    }

    [SlashCommand("allrolemodules", "Enable or disable all modules for a role"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task AllRoleMdls(PermissionSlash action, IRole role)
    {
        if (role == role.Guild.EveryoneRole)
            return;

        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Role,
            PrimaryTargetId = role.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = Convert.ToBoolean((int)action) ? 1 : 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("arm_enable",
                Format.Code(role.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("arm_disable",
                Format.Code(role.Name)).ConfigureAwait(false);
        }
    }

    [SlashCommand("allusermodules", "Enable or disable all modules for a user"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task AllUsrMdls(PermissionSlash action, IUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = Convert.ToBoolean((int)action) ? 1 : 0
        }).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
        {
            await ReplyConfirmLocalizedAsync("aum_enable",
                Format.Code(user.ToString())).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("aum_disable",
                Format.Code(user.ToString())).ConfigureAwait(false);
        }
    }

    [SlashCommand("allservermodules", "Enable or disable all modules in the server"),
     Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task AllSrvrMdls(PermissionSlash action)
    {
        var newPerm = new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = Convert.ToBoolean((int)action) ? 1 : 0
        };

        var allowUser = new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = ctx.User.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = 1
        };

        await Service.AddPermissions(ctx.Guild.Id,
            newPerm,
            allowUser).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
            await ReplyConfirmLocalizedAsync("asm_enable").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("asm_disable").ConfigureAwait(false);
    }

    [ComponentInteraction("permenu_update.*", true)]
    [Discord.Interactions.RequireUserPermission(GuildPermission.Administrator)]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    public async Task UpdateMessageWithPermenu(string commandName)
    {
        IList<Permissionv2> perms = Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache)
            ? permCache.Permissions.Source.ToList()
            : Permissionv2.GetDefaultPermlist;

        var effecting = perms.Where(x => x.SecondaryTargetName == commandName);
        var dpoUsed = dpoS.TryGetOverrides(ctx.Guild.Id, commandName, out _);


        var cb = new ComponentBuilder()
            .WithButton(GetText("perm_quick_options"),
                "Often I am upset That I cannot fall in love but I guess This avoids the stress of falling out of it",
                ButtonStyle.Secondary,
                Emote.Parse("<:IconSettings:778931333459738626>"), disabled: true)
            .WithButton(GetText("back"), $"help_component_restore.{commandName}",
                emote: "<:perms_back_arrow:1085352564943491102>".ToIEmote());

        var quickEmbeds = (Context.Interaction as SocketMessageComponent).Message.Embeds
            .Where(x => x.Footer.GetValueOrDefault().Text != "$$mdk_redperm$$").ToArray();

        // check effecting for redundant permissions
        var redundant = effecting.Where(x => effecting.Any(y =>
            y.Id != x.Id &&
            y.PrimaryTarget == x.PrimaryTarget &&
            y.PrimaryTargetId == x.PrimaryTargetId &&
            y.SecondaryTarget == x.SecondaryTarget &&
            y.SecondaryTargetName == x.SecondaryTargetName)).ToList();
        if (redundant.Count >= 1)
        {
            redundant = redundant
                .DistinctBy(x => (x.PrimaryTarget, x.PrimaryTargetId, x.SecondaryTarget, x.SecondaryTargetName))
                .ToList();

            var eb = new EmbedBuilder()
                .WithTitle(GetText("perm_quick_options_redundant"))
                .WithColor(0xe52d00)
                .WithDescription(GetText("perm_quick_options_redundant_explainer"))
                .AddField(GetText("perm_quick_options_redundant_count"), redundant.Count)
                .WithFooter("$$mdk_redperm$$");

            cb.WithButton(GetText("perm_quick_options_redundant_resolve"), $"credperms.{commandName}",
                ButtonStyle.Success);

            await (Context.Interaction as SocketMessageComponent).UpdateAsync(x =>
            {
                x.Components = cb.Build();
                x.Embeds = quickEmbeds.Append(eb.Build()).ToArray();
            });
            return;
        }

        if (effecting.Any(x => x.PrimaryTarget == PrimaryPermissionType.Server && x.State == 0))
            cb.WithButton(GetText("perm_quick_options_disable_disabled"), $"command_toggle_disable.{commandName}",
                ButtonStyle.Success,
                "<:perms_check:1085356998247317514>".ToIEmote());
        else
            cb.WithButton(GetText("perm_quick_options_disable_enabled"), $"command_toggle_disable.{commandName}",
                ButtonStyle.Danger,
                "<:perms_disabled:1085358511900327956>".ToIEmote());

        if (effecting.Any() || dpoUsed)
            cb.WithButton(GetText("local_perms_reset"), $"local_perms_reset.{commandName}", ButtonStyle.Danger,
                "<:perms_warning:1085356999824396308>".ToIEmote());

        cb.WithSelectMenu($"cmd_perm_spawner.{commandName}", new List<SelectMenuOptionBuilder>
        {
            new(GetText("cmd_perm_spawner_required_perms"), "dpo", GetText("cmd_perm_spawner_required_perms_desc"),
                "<:perms_dpo:1085338505464512595>".ToIEmote()),
            new(GetText("cmd_perm_spawner_user_perms"), "usr", GetText("cmd_perm_spawner_user_perms_desc"),
                "<:perms_user_perms:1085426466818359367>".ToIEmote()),
            new(GetText("cmd_perm_spawner_role_perms"), "rol", GetText("cmd_perm_spawner_role_perms_desc"),
                "<:role:808826577785716756>".ToIEmote()),
            new(GetText("cmd_perm_spawner_channel_perms"), "chn", GetText("cmd_perm_spawner_channel_perms_desc"),
                "<:ChannelText:779036156175188001>".ToIEmote()),
            new(GetText("cmd_perm_spawner_category_perms"), "cat", GetText("cmd_perm_spawner_category_perms_desc"),
                GetText("not_an_easter_egg").ToIEmote())
        }, GetText("advanced_options"));

        await RespondAsync(components: cb.Build(), embeds: quickEmbeds, ephemeral: true);
    }

    [ComponentInteraction("credperms.*", true)]
    [Discord.Interactions.RequireUserPermission(GuildPermission.Administrator)]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    public async Task ClearRedundantPerms(string commandName)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        var quickEmbeds = (Context.Interaction as SocketMessageComponent).Message.Embeds
            .Where(x => x.Footer.GetValueOrDefault().Text != "$$mdk_redperm$$").ToArray();

        var redundant = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => perms.Where(x => x.SecondaryTargetName == commandName).Any(y =>
                y.Id != x.Id &&
                y.PrimaryTarget == x.PrimaryTarget &&
                y.PrimaryTargetId == x.PrimaryTargetId &&
                y.SecondaryTarget == x.SecondaryTarget &&
                y.SecondaryTargetName == x.SecondaryTargetName))
            .DistinctBy(x => (x.PrimaryTarget, x.PrimaryTargetId, x.SecondaryTarget, x.SecondaryTargetName))
            .ToList();

        if (redundant.Count == 0)
        {
            await UpdateMessageWithPermenu(commandName);
            return;
        }

        var perm = redundant.First();

        var cb = new ComponentBuilder()
            .WithSelectMenu(
                $"credperms_m.{((int)perm.PrimaryTarget)}.{perm.PrimaryTargetId}.{((int)perm.SecondaryTarget)}.{perm.SecondaryTargetName}",
                new List<SelectMenuOptionBuilder>()
                {
                    new(GetText("perm_quick_options_redundant_tool_enable"), "enabled",
                        GetText("perm_quick_options_redundant_tool_enabled_description")),
                    new(GetText("perm_quick_options_redundant_tool_disable"), "disabled",
                        GetText("perm_quick_options_redundant_tool_disable_description")),
                    new(GetText("perm_quick_options_redundant_tool_clear"), "clear",
                        GetText("perm_quick_options_redundant_tool_clear_description")),
                    new(GetText("perm_quick_options_redundant_tool_current"), "current",
                        GetText("perm_quick_options_redundant_tool_current_description")),
                }, "Action");

        var eb = new EmbedBuilder()
            .WithTitle(GetText("perm_quick_options_redundant"))
            .WithDescription(GetText("perm_quick_options_redundant_tool_priority_disclaimer"))
            .AddField(GetText("perm_quick_options_redundant_tool_ptar"), perm.PrimaryTarget.ToString(), true)
            .AddField(GetText("perm_quick_options_redundant_tool_ptarid"),
                $"{perm.PrimaryTargetId} ({PermissionService.MentionPerm(perm.PrimaryTarget, perm.PrimaryTargetId)})",
                true)
            .AddField(GetText("perm_quick_options_redundant_tool_custom"), perm.IsCustomCommand)
            .AddField(GetText("perm_quick_options_redundant_tool_star"), perm.SecondaryTarget.ToString(), true)
            .AddField(GetText("perm_quick_options_redundant_tool_starid"), $"{perm.SecondaryTargetName}", true)
            .WithFooter("$$mdk_redperm$$")
            .WithColor(0xe52d00);

        await (Context.Interaction as SocketMessageComponent).UpdateAsync(x =>
        {
            x.Embeds = quickEmbeds.Append(eb.Build()).ToArray();
            x.Components = cb.Build();
        });
    }

    [ComponentInteraction("credperms_m.*.*.*.*", true)]
    [Discord.Interactions.RequireUserPermission(GuildPermission.Administrator)]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    public async Task ResolvePermMenu(string primaryTargetType, string primaryTargetIdRaw, string secondaryTargetType,
        string secondaryTargetId)
    {
        var primaryTarget = (PrimaryPermissionType)Convert.ToInt32(primaryTargetType);
        var primaryTargetId = Convert.ToUInt64(primaryTargetIdRaw);
        var secondaryTarget = (SecondaryPermissionType)Convert.ToInt32(secondaryTargetType);

        // get all effected perms
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        // already ordered by index
        var effected = perms.Where(x =>
            x.PrimaryTarget == primaryTarget &&
            x.PrimaryTargetId == primaryTargetId &&
            x.SecondaryTarget == secondaryTarget &&
            x.SecondaryTargetName == secondaryTargetId);

        var selected = (Context.Interaction as SocketMessageComponent).Data.Values.First();
        var first = effected.First();
        var indexmod = -1;
        switch (selected)
        {
            case "clear":
                effected.ForEach(async x => await Service.RemovePerm(ctx.Guild.Id, x.Index - ++indexmod));
                break;
            case "current":
                effected
                    .Where(x => x != first)
                    .ForEach(async x => await Service.RemovePerm(ctx.Guild.Id, x.Index - ++indexmod));
                break;
            case "enabled":
                effected
                    .Where(x => x != first)
                    .ForEach(async x => await Service.RemovePerm(ctx.Guild.Id, x.Index - ++indexmod));
                await Service.UpdatePerm(ctx.Guild.Id, first.Index, true);
                break;
            case "disabled":
                effected
                    .Where(x => x != first)
                    .ForEach(async x => await Service.RemovePerm(ctx.Guild.Id, x.Index - ++indexmod));
                await Service.UpdatePerm(ctx.Guild.Id, first.Index, false);
                break;
        }

        await ClearRedundantPerms(secondaryTargetId);
    }

    [ComponentInteraction("command_toggle_disable.*", true)]
    [Discord.Interactions.RequireUserPermission(GuildPermission.Administrator)]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    public async Task ToggleCommanddisabled(string commandName)
    {
        await using var uow = db.GetDbContext();

        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .ToList();

        var sc = perms
            .FirstOrDefault(x => x.PrimaryTarget == PrimaryPermissionType.Server, null);

        if (sc is not null && sc.State == 1)
        {
            await Service.RemovePerm(ctx.Guild.Id, sc.Index);
            sc = null;
        }

        if (sc is null)
        {
            await Service.AddPermissions(ctx.Guild.Id, new Permissionv2()
            {
                GuildConfigId = uow.ForGuildId(ctx.Guild.Id).Id,
                IsCustomCommand = 0,
                PrimaryTarget = PrimaryPermissionType.Server,
                PrimaryTargetId = 0,
                SecondaryTarget = SecondaryPermissionType.Command,
                SecondaryTargetName = commandName,
                State = 0
            });

            // reset local cache
            if (Service.Cache.TryGetValue(ctx.Guild.Id, out permCache))
                perms = permCache.Permissions.Source.ToList();
            else
                perms = Permissionv2.GetDefaultPermlist;

            perms = perms
                .Where(x => x.SecondaryTargetName == commandName)
                .ToList();

            var index = perms.First(x => x.PrimaryTarget == PrimaryPermissionType.Server).Index;

            await Service.UnsafeMovePerm(ctx.Guild.Id, index, 1);

            await UpdateMessageWithPermenu(commandName);
            return;
        }

        await Service.RemovePerm(ctx.Guild.Id, sc.Index);
        await UpdateMessageWithPermenu(commandName);
    }

    [ComponentInteraction("local_perms_reset.*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task LocalPermsReset(string commandName)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        var effecting = perms.Where(x => x.SecondaryTargetName == commandName);

        var indexmod = -1;
        effecting.ForEach(async x => await Service.RemovePerm(ctx.Guild.Id, x.Index - ++indexmod));

        if (dpoS.TryGetOverrides(ctx.Guild.Id, commandName, out _))
            _ = dpoS.RemoveOverride(ctx.Guild.Id, commandName);

        return UpdateMessageWithPermenu(commandName);
    }

    [ComponentInteraction("cmd_perm_spawner.*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermSpawner(string commandName, string[] values) => values.First() switch
    {
        "dpo" => CommandPermsDpo(commandName),
        "usr" => CommandPermsUsr(commandName, true, true, ""),
        "rol" => CommandPermsRol(commandName, true, true, ""),
        "chn" => CommandPermsChn(commandName, true, true, ""),
        "cat" => CommandPermsCat(commandName, true, true, ""),
        _ => UpdateMessageWithPermenu(commandName)
    };


    [ComponentInteraction("cmd_perm_spawner_dpo.*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermsDpo(string commandName)
    {
        var perms = Enum.GetValues<GuildPermission>();
        List<SelectMenuBuilder> selects = new();

        dpoS.TryGetOverrides(ctx.Guild.Id, commandName, out var effecting);

        var info = cmdServe.Commands.First(x => x.Name == commandName);
        var userPerm = ((TextUserPermAttribute)info.Preconditions.FirstOrDefault(ca => ca is TextUserPermAttribute))
            ?.UserPermissionAttribute.GuildPermission;

        var basePerms = userPerm is not null
            ? perms.Where(x => (userPerm & x) == x).ToList()
            : new();
        var truePerms = perms.Where(x => (effecting & x) == x);

        if (!truePerms.Any())
            truePerms = basePerms;
        for (var i = 0; i < 25 && ((selects.Count - 1) * 25) < perms.Length && selects.Count <= 5; i++)
        {
            selects.Add(new SelectMenuBuilder()
                .WithCustomId($"update_cmd_dpo.{commandName}${i}")
                .WithMinValues(0)
                .WithPlaceholder(GetText("cmd_perm_spawner_dpo_page", selects.Count + 1)));
            var current = selects.Last();
            for (var j = 0; j < 25 && ((selects.Count - 1) * 25) + j < perms.Length; j++)
            {
                var cdat = perms[((selects.Count - 1) * 25) + j];
                current.AddOption(cdat.ToString(), ((ulong)cdat).ToString(), cdat.ToString(),
                    isDefault: truePerms.Any(x => x == cdat));
                current.MaxValues = j + 1;
            }
        }

        var cb = new ComponentBuilder()
            .WithRows(selects.Where(x => x.Options.Count > 0).Select(x => new ActionRowBuilder().WithSelectMenu(x)))
            .WithButton(GetText("back"), $"permenu_update.{commandName}",
                emote: "<:perms_back_arrow:1085352564943491102>".ToIEmote());

        return (ctx.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }

    [ComponentInteraction("update_cmd_dpo.*$*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task UpdateCommandDpo(string commandName, int index, string[] values)
    {
        // get list of command perms
        var perms = Enum.GetValues<GuildPermission>();

        dpoS.TryGetOverrides(ctx.Guild.Id, commandName, out var effecting);

        var info = cmdServe.Commands.First(x => x.Name == commandName);
        var userPerm = ((TextUserPermAttribute)info.Preconditions.FirstOrDefault(ca => ca is TextUserPermAttribute))
            ?.UserPermissionAttribute.GuildPermission;

        var basePerms = userPerm is not null
            ? perms.Where(x => (userPerm & x) == x).ToList()
            : new();
        var truePerms = perms.Where(x => (effecting & x) == x).ToList();
        // get list of selectable perms
        var selectable = perms.Skip(25 * index).Take(25).ToList();
        // get list of selected perms
        var selected = values.Select(x => (GuildPermission)Convert.ToUInt64(x)).ToList();
        // remove selectable from command perms
        var updatedPerms = truePerms.Where(x => !selectable.Contains(x)).ToList();
        // add selected to command perms
        updatedPerms.AddRange(selected);
        // update perms
        await dpoS.RemoveOverride(ctx.Guild.Id, commandName);
        await dpoS.AddOverride(ctx.Guild.Id, commandName, updatedPerms.Aggregate((x, y) => x |= y));
        await CommandPermsDpo(commandName);
    }

    [ComponentInteraction("command_perm_spawner_usr.*.*.*$*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermsUsr(string commandName, bool overwright, bool allow, string _)
    {
        // perm testing code, quickly add dummy allow or deny objects to the end of the perm list
        // please do not remove or enable without dissabling before commiting

#if FORCE_ADD_DUMMY_PERMS
        var nperms = new List<Permissionv2>();
        for (var ni = 0; ni < 50; ni++)
        {
            nperms.Add(new()
            {
                IsCustomCommand = /*true*/false,
                PrimaryTarget = PrimaryPermissionType.User,
                PrimaryTargetId = (ulong)ni,
                SecondaryTarget = SecondaryPermissionType.Command,
                SecondaryTargetName = commandName
            });
        }
        await Service.AddPermissions(Context.Guild.Id, nperms.ToArray());
#endif
        // get perm overwrights targeting users
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.User)
            .Where(x => x.State == 1)
            .ToList();
        // chunk into groups of 25, take first three
        var splitGroups = perms
            .Select((x, i) => (x, i))
            .GroupBy(x => x.i / 25)
            .Select(x =>
                x.Select(y => y.x))
            .Take(3)
            .ToList();
        // make component builders, slack fill with blank user selects
        var cb = new ComponentBuilder()
            .WithButton(GetText("back"), $"permenu_update.{commandName}",
                emote: "<:perms_back_arrow:1085352564943491102>".ToIEmote())
            .WithButton(GetText("perm_quick_options_overwright"),
                $"command_perm_spawner_usr.{commandName}.{true}.{allow}$1",
                overwright ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_overwright:1085421377798029393>".ToIEmote(), disabled: overwright)
            .WithButton(GetText("perm_quick_options_fallback"),
                $"command_perm_spawner_usr.{commandName}.{false}.{allow}$2",
                !overwright ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_fallback:1085421376032231444>".ToIEmote(), disabled: !overwright)
            .WithButton(GetText("perm_quick_options_allow"),
                $"command_perm_spawner_usr.{commandName}.{overwright}.{true}$3",
                allow ? ButtonStyle.Success : ButtonStyle.Secondary,
                "<:perms_check:1085356998247317514>".ToIEmote(), disabled: allow)
            .WithButton(GetText("perm_quick_options_deny"),
                $"command_perm_spawner_usr.{commandName}.{overwright}.{false}$4",
                !allow ? ButtonStyle.Danger : ButtonStyle.Secondary,
                "<:perms_disabled:1085358511900327956>".ToIEmote(), disabled: !allow);

        var i = 0;
        for (i = 0; i < Math.Min(splitGroups.Count, 3); i++)
        {
            var options = splitGroups[i]
                .Select(async x => (x, user: (await TryGetUser(x.PrimaryTargetId))))
                .Select(x => x.Result)
                .Select(x => new SelectMenuOptionBuilder(x.user?.ToString() ?? "Unknown#0000", x.x.Id.ToString(),
                    GetText($"perms_quick_options_user_remove_{(allow ? "allow" : "deny")}", x.x.PrimaryTargetId),
                    "<:perms_user_perms:1085426466818359367>".ToIEmote(), true));
            var sb = new SelectMenuBuilder($"perm_quick_options_user_remove.{commandName}.{overwright}.{allow}${i}",
                options.ToList(), GetText("perms_quick_options_user_remove"),
                options.Count(), 0);
            cb.WithSelectMenu(sb);
        }

        cb.WithSelectMenu(
            $"perm_quick_options_user_add.{commandName}.{overwright}.{allow}${Random.Shared.NextInt64(i, long.MaxValue)}",
            placeholder: GetText("perm_quick_options_add_users"), minValues: 1, maxValues: 10,
            type: ComponentType.UserSelect, options: null);

        return (Context.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }

    [ComponentInteraction("perm_quick_options_user_remove.*.*.*$*", true)]
    public async Task RemoveUserOveride(string commandName, bool overwright, bool allow, int index, string[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.User)
            .Where(x => x.State == 1)
            .ToList();
        // chunk into groups of 25, take first three
        var splitGroups = perms
            .Select((x, i) => (x, i))
            .GroupBy(x => x.i / 25)
            .Select(x =>
                x.Select(y => y.x)
                    .ToList())
            .ToList();

        perms = splitGroups[index];

        var i = -1;
        foreach (var p in (perms.Where(x => !values.Contains(x.Id.ToString()))))
        {
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);
        }

        await CommandPermsUsr(commandName, overwright, allow, "");
    }

    [ComponentInteraction("perm_quick_options_user_add.*.*.*$*", true)]
    public async Task AddUserOveride(string commandName, bool overwright, bool allow, string _, IUser[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        var matchingPerms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.User)
            .Where(x => x.State == 1)
            .ToList();

        var needMod = values.Where(x => !matchingPerms.Any(y => y.PrimaryTargetId == x.Id));
        var needRems = perms.Where(x => needMod.Any(y => x.PrimaryTargetId == y.Id));
        var needAdd = needMod.Where(x => !needRems.Any(y => x.Id == y.PrimaryTargetId));

        var i = -1;
        foreach (var p in needRems)
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);

        var trueAdd = needAdd.Select(x => new Permissionv2()
        {
            IsCustomCommand = 0,
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = x.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = commandName,
            State = 1
        });
        await Service.AddPermissions(ctx.Guild.Id, trueAdd.ToArray());

        if (!overwright)
        {
            await CommandPermsUsr(commandName, overwright, allow, "");
            return;
        }

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        for (i = 0; i < needAdd.Count(); i++)
            await Service.UnsafeMovePerm(ctx.Guild.Id, perms.Last().Index, 1);
        await CommandPermsUsr(commandName, overwright, allow, "");
    }


    [ComponentInteraction("help_component_restore.*", true)]
    public Task HelpComponentRestore(string commandName)
    {
        var cb = new ComponentBuilder()
            .WithButton(GetText("help_run_cmd"), $"runcmd.{commandName}", ButtonStyle.Success)
            .WithButton(GetText("help_permenu_link"), $"permenu_update.{commandName}", ButtonStyle.Primary,
                Emote.Parse("<:IconPrivacySettings:845090111976636446>"));
        return (ctx.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }

    [ComponentInteraction("command_perm_spawner_rol.*.*.*$*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermsRol(string commandName, bool overwright, bool allow, string _)
    {
        // perm testing code, quickly add dummy allow or deny objects to the end of the perm list
        // please do not remove or enable without dissabling before commiting

#if FORCE_ADD_DUMMY_PERMS
        var nperms = new List<Permissionv2>();
        for (var ni = 0; ni < 50; ni++)
        {
            nperms.Add(new()
            {
                IsCustomCommand = /*true*/false,
                PrimaryTarget = PrimaryPermissionType.Role,
                PrimaryTargetId = (ulong)ni,
                SecondaryTarget = SecondaryPermissionType.Command,
                SecondaryTargetName = commandName
            });
        }
        await Service.AddPermissions(Context.Guild.Id, nperms.ToArray());
#endif
        // get perm overwrights targeting roles
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Role)
            .Where(x => x.State == 1)
            .ToList();
        // chunk into groups of 25, take first three
        var splitGroups = perms
            .Select((x, i) => (x, i))
            .GroupBy(x => x.i / 25)
            .Select(x =>
                x.Select(y => y.x))
            .Take(3)
            .ToList();
        // make component builders, slack fill with blank role selects
        var cb = new ComponentBuilder()
            .WithButton(GetText("back"), $"permenu_update.{commandName}",
                emote: "<:perms_back_arrow:1085352564943491102>".ToIEmote())
            .WithButton(GetText("perm_quick_options_overwright"),
                $"command_perm_spawner_rol.{commandName}.{true}.{allow}$1",
                overwright ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_overwright:1085421377798029393>".ToIEmote(), disabled: overwright)
            .WithButton(GetText("perm_quick_options_fallback"),
                $"command_perm_spawner_rol.{commandName}.{false}.{allow}$2",
                !overwright ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_fallback:1085421376032231444>".ToIEmote(), disabled: !overwright)
            .WithButton(GetText("perm_quick_options_allow"),
                $"command_perm_spawner_rol.{commandName}.{overwright}.{true}$3",
                allow ? ButtonStyle.Success : ButtonStyle.Secondary,
                "<:perms_check:1085356998247317514>".ToIEmote(), disabled: allow)
            .WithButton(GetText("perm_quick_options_deny"),
                $"command_perm_spawner_rol.{commandName}.{overwright}.{false}$4",
                !allow ? ButtonStyle.Danger : ButtonStyle.Secondary,
                "<:perms_disabled:1085358511900327956>".ToIEmote(), disabled: !allow);

        var i = 0;
        for (i = 0; i < Math.Min(splitGroups.Count, 3); i++)
        {
            var options = splitGroups[i]
                .Select(x => (x, role: (TryGetRole(x.PrimaryTargetId))))
                .Select(x => new SelectMenuOptionBuilder(x.role?.ToString() ?? "Deleted Role", x.x.Id.ToString(),
                    GetText($"perms_quick_options_role_remove_{(allow ? "allow" : "deny")}", x.x.PrimaryTargetId),
                    "<:role:808826577785716756>".ToIEmote(), true));
            var sb = new SelectMenuBuilder($"perm_quick_options_role_remove.{commandName}.{overwright}.{allow}${i}",
                options.ToList(), GetText("perms_quick_options_role_remove"),
                options.Count(), 0);
            cb.WithSelectMenu(sb);
        }

        cb.WithSelectMenu(
            $"perm_quick_options_role_add.{commandName}.{overwright}.{allow}${Random.Shared.NextInt64(i, long.MaxValue)}",
            placeholder: GetText("perm_quick_options_add_roles"), minValues: 1, maxValues: 10,
            type: ComponentType.RoleSelect, options: null);

        return (Context.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }

    [ComponentInteraction("perm_quick_options_role_remove.*.*.*$*", true)]
    public async Task RemoveRoleOveride(string commandName, bool overwright, bool allow, int index, string[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Role)
            .Where(x => x.State == 1)
            .ToList();
        // chunk into groups of 25, take first three
        var splitGroups = perms
            .Select((x, i) => (x, i))
            .GroupBy(x => x.i / 25)
            .Select(x =>
                x.Select(y => y.x)
                    .ToList())
            .ToList();

        perms = splitGroups[index];

        var i = -1;
        foreach (var p in (perms.Where(x => !values.Contains(x.Id.ToString()))))
        {
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);
        }

        await CommandPermsRol(commandName, overwright, allow, "");
    }

    [ComponentInteraction("perm_quick_options_role_add.*.*.*$*", true)]
    public async Task AddRoleOveride(string commandName, bool overwright, bool allow, string _, IRole[] values)
    {
        IList<Permissionv2> perms = Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache)
            ? permCache.Permissions.Source.ToList()
            : Permissionv2.GetDefaultPermlist;

        var matchingPerms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Role)
            .Where(x => x.State == 1)
            .ToList();

        var needMod = values.Where(x => !matchingPerms.Any(y => y.PrimaryTargetId == x.Id));
        var needRems = perms.Where(x => needMod.Any(y => x.PrimaryTargetId == y.Id));
        var needAdd = needMod.Where(x => !needRems.Any(y => x.Id == y.PrimaryTargetId));

        var i = -1;
        foreach (var p in needRems)
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);

        var trueAdd = needAdd.Select(x => new Permissionv2()
        {
            IsCustomCommand = 0,
            PrimaryTarget = PrimaryPermissionType.Role,
            PrimaryTargetId = x.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = commandName,
            State = 1
        });
        await Service.AddPermissions(ctx.Guild.Id, trueAdd.ToArray());

        if (!overwright)
        {
            await CommandPermsRol(commandName, overwright, allow, "");
            return;
        }

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        for (i = 0; i < needAdd.Count(); i++)
            await Service.UnsafeMovePerm(ctx.Guild.Id, perms.Last().Index, 1);
        await CommandPermsRol(commandName, overwright, allow, "");
    }


    [ComponentInteraction("command_perm_spawner_chn.*.*.*$*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermsChn(string commandName, bool overwright, bool allow, string _)
    {
        // perm testing code, quickly add dummy allow or deny objects to the end of the perm list
        // please do not remove or enable without dissabling before commiting

#if FORCE_ADD_DUMMY_PERMS
        var nperms = new List<Permissionv2>();
        for (var ni = 0; ni < 50; ni++)
        {
            nperms.Add(new()
            {
                IsCustomCommand = /*true*/false,
                PrimaryTarget = PrimaryPermissionType.Channel,
                PrimaryTargetId = (ulong)ni,
                SecondaryTarget = SecondaryPermissionType.Command,
                SecondaryTargetName = commandName
            });
        }
        await Service.AddPermissions(Context.Guild.Id, nperms.ToArray());
#endif
        // get perm overwrights targeting users

        IList<Permissionv2> perms = Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache)
            ? permCache.Permissions.Source.ToList()
            : Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Channel)
            .Where(x => x.State == 1)
            .ToList();
        // chunk into groups of 25, take first three
        var splitGroups = perms
            .Select((x, i) => (x, i))
            .GroupBy(x => x.i / 25)
            .Select(x =>
                x.Select(y => y.x))
            .Take(3)
            .ToList();
        // make component builders, slack fill with blank user selects
        var cb = new ComponentBuilder()
            .WithButton(GetText("back"), $"permenu_update.{commandName}",
                emote: "<:perms_back_arrow:1085352564943491102>".ToIEmote())
            .WithButton(GetText("perm_quick_options_overwright"),
                $"command_perm_spawner_chn.{commandName}.{true}.{allow}$1",
                overwright ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_overwright:1085421377798029393>".ToIEmote(), disabled: overwright)
            .WithButton(GetText("perm_quick_options_fallback"),
                $"command_perm_spawner_chn.{commandName}.{false}.{allow}$2",
                !overwright ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_fallback:1085421376032231444>".ToIEmote(), disabled: !overwright)
            .WithButton(GetText("perm_quick_options_allow"),
                $"command_perm_spawner_chn.{commandName}.{overwright}.{true}$3",
                allow ? ButtonStyle.Success : ButtonStyle.Secondary,
                "<:perms_check:1085356998247317514>".ToIEmote(), disabled: allow)
            .WithButton(GetText("perm_quick_options_deny"),
                $"command_perm_spawner_chn.{commandName}.{overwright}.{false}$4",
                !allow ? ButtonStyle.Danger : ButtonStyle.Secondary,
                "<:perms_disabled:1085358511900327956>".ToIEmote(), disabled: !allow);

        var i = 0;
        for (i = 0; i < Math.Min(splitGroups.Count, 3); i++)
        {
            var options = splitGroups[i]
                .Select(async x => (x, channel: await (TryGetChannel(x.PrimaryTargetId))))
                .Select(x => x.Result)
                .Select(x => new SelectMenuOptionBuilder(x.channel?.ToString() ?? "Deleted Channel", x.x.Id.ToString(),
                    GetText($"perms_quick_options_channel_remove_{(allow ? "allow" : "deny")}", x.x.PrimaryTargetId),
                    GetChannelEmote(x.channel), true));
            var sb = new SelectMenuBuilder($"perm_quick_options_channel_remove.{commandName}.{overwright}.{allow}${i}",
                options.ToList(),
                GetText("perms_quick_options_channel_remove"), options.Count(), 0);
            cb.WithSelectMenu(sb);
        }

        cb.WithSelectMenu(
            $"perm_quick_options_channel_add.{commandName}.{overwright}.{allow}${Random.Shared.NextInt64(i, long.MaxValue)}",
            placeholder: GetText("perm_quick_options_add_channels"), minValues: 1, maxValues: 10,
            type: ComponentType.ChannelSelect, options: null,
            channelTypes: Enum.GetValues<ChannelType>().Where(x => x != ChannelType.Category).ToArray());

        return (Context.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }


    [ComponentInteraction("perm_quick_options_channel_remove.*.*.*$*", true)]
    public async Task RemoveChannelOveride(string commandName, bool overwright, bool allow, int index, string[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Channel)
            .Where(x => x.State == 1)
            .ToList();
        // chunk into groups of 25, take first three
        var splitGroups = perms
            .Select((x, i) => (x, i))
            .GroupBy(x => x.i / 25)
            .Select(x =>
                x.Select(y => y.x)
                    .ToList())
            .ToList();

        perms = splitGroups[index];

        var i = -1;
        foreach (var p in (perms.Where(x => !values.Contains(x.Id.ToString()))))
        {
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);
        }

        await CommandPermsChn(commandName, overwright, allow, "");
    }

    [ComponentInteraction("perm_quick_options_channel_add.*.*.*$*", true)]
    public async Task AddChannelOveride(string commandName, bool overwright, bool allow, string _, IChannel[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        var matchingPerms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Channel)
            .Where(x => x.State == 1)
            .ToList();

        var needMod = values.Where(x => !matchingPerms.Any(y => y.PrimaryTargetId == x.Id));
        var needRems = perms.Where(x => needMod.Any(y => x.PrimaryTargetId == y.Id));
        var needAdd = needMod.Where(x => !needRems.Any(y => x.Id == y.PrimaryTargetId));

        var i = -1;
        foreach (var p in needRems)
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);

        var trueAdd = needAdd.Select(x => new Permissionv2()
        {
            IsCustomCommand = 0,
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = x.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = commandName,
            State = 1
        });
        await Service.AddPermissions(ctx.Guild.Id, trueAdd.ToArray());

        if (!overwright)
        {
            await CommandPermsChn(commandName, overwright, allow, "");
            return;
        }

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        for (i = 0; i < needAdd.Count(); i++)
            await Service.UnsafeMovePerm(ctx.Guild.Id, perms.Last().Index, 1);
        await CommandPermsChn(commandName, overwright, allow, "");
    }


    [ComponentInteraction("command_perm_spawner_cat.*.*.*$*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermsCat(string commandName, bool overwright, bool allow, string _)
    {
        // perm testing code, quickly add dummy allow or deny objects to the end of the perm list
        // please do not remove or enable without dissabling before commiting

#if FORCE_ADD_DUMMY_PERMS
        var nperms = new List<Permissionv2>();
        for (var ni = 0; ni < 50; ni++)
        {
            nperms.Add(new()
            {
                IsCustomCommand = /*true*/false,
                PrimaryTarget = PrimaryPermissionType.Category,
                PrimaryTargetId = (ulong)ni,
                SecondaryTarget = SecondaryPermissionType.Command,
                SecondaryTargetName = commandName
            });
        }
        await Service.AddPermissions(Context.Guild.Id, nperms.ToArray());
#endif
        // get perm overwrights targeting users
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Category)
            .Where(x => x.State == 1)
            .ToList();
        // chunk into groups of 25, take first three
        var splitGroups = perms
            .Select((x, i) => (x, i))
            .GroupBy(x => x.i / 25)
            .Select(x =>
                x.Select(y => y.x))
            .Take(3)
            .ToList();
        // make component builders, slack fill with blank user selects
        var cb = new ComponentBuilder()
            .WithButton(GetText("back"), $"permenu_update.{commandName}",
                emote: "<:perms_back_arrow:1085352564943491102>".ToIEmote())
            .WithButton(GetText("perm_quick_options_overwright"),
                $"command_perm_spawner_cat.{commandName}.{true}.{allow}$1",
                overwright ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_overwright:1085421377798029393>".ToIEmote(), disabled: overwright)
            .WithButton(GetText("perm_quick_options_fallback"),
                $"command_perm_spawner_cat.{commandName}.{false}.{allow}$2",
                !overwright ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_fallback:1085421376032231444>".ToIEmote(), disabled: !overwright)
            .WithButton(GetText("perm_quick_options_allow"),
                $"command_perm_spawner_cat.{commandName}.{overwright}.{true}$3",
                allow ? ButtonStyle.Success : ButtonStyle.Secondary,
                "<:perms_check:1085356998247317514>".ToIEmote(), disabled: allow)
            .WithButton(GetText("perm_quick_options_deny"),
                $"command_perm_spawner_cat.{commandName}.{overwright}.{false}$4",
                !allow ? ButtonStyle.Danger : ButtonStyle.Secondary,
                "<:perms_disabled:1085358511900327956>".ToIEmote(), disabled: !allow);

        var i = 0;
        for (i = 0; i < Math.Min(splitGroups.Count, 3); i++)
        {
            var options = splitGroups[i]
                .Select(async x => (x, channel: await (TryGetChannel(x.PrimaryTargetId))))
                .Select(x => x.Result)
                .Select(x => new SelectMenuOptionBuilder(x.channel?.ToString() ?? "Deleted Channel", x.x.Id.ToString(),
                    GetText($"perms_quick_options_category_remove_{(allow ? "allow" : "deny")}", x.x.PrimaryTargetId),
                    GetChannelEmote(x.channel), true));
            var sb = new SelectMenuBuilder($"perm_quick_options_category_remove.{commandName}.{overwright}.{allow}${i}",
                options.ToList(),
                GetText("perms_quick_options_category_remove"), options.Count(), 0);
            cb.WithSelectMenu(sb);
        }

        cb.WithSelectMenu(
            $"perm_quick_options_category_add.{commandName}.{overwright}.{allow}${Random.Shared.NextInt64(i, long.MaxValue)}",
            placeholder: GetText("perm_quick_options_add_categories"), minValues: 1, maxValues: 10,
            type: ComponentType.ChannelSelect, options: null, channelTypes: new[]
            {
                ChannelType.Category
            });

        return (Context.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }


    [ComponentInteraction("perm_quick_options_category_remove.*.*.*$*", true)]
    public async Task RemoveCategoryOveride(string commandName, bool overwright, bool allow, int index, string[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Category)
            .Where(x => x.State == 1)
            .ToList();
        // chunk into groups of 25, take first three
        var splitGroups = perms
            .Select((x, i) => (x, i))
            .GroupBy(x => x.i / 25)
            .Select(x =>
                x.Select(y => y.x)
                    .ToList())
            .ToList();

        perms = splitGroups[index];

        var i = -1;
        foreach (var p in (perms.Where(x => !values.Contains(x.Id.ToString()))))
        {
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);
        }

        await CommandPermsChn(commandName, overwright, allow, "");
    }

    [ComponentInteraction("perm_quick_options_category_add.*.*.*$*", true)]
    public async Task AddCategoryOveride(string commandName, bool overwright, bool allow, string _, IChannel[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        var matchingPerms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Category)
            .Where(x => x.State == 1)
            .ToList();

        var needMod = values.Where(x => !matchingPerms.Any(y => y.PrimaryTargetId == x.Id));
        var needRems = perms.Where(x => needMod.Any(y => x.PrimaryTargetId == y.Id));
        var needAdd = needMod.Where(x => !needRems.Any(y => x.Id == y.PrimaryTargetId));

        var i = -1;
        foreach (var p in needRems)
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);

        var trueAdd = needAdd.Select(x => new Permissionv2()
        {
            IsCustomCommand = 0,
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = x.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = commandName,
            State = 1
        });
        await Service.AddPermissions(ctx.Guild.Id, trueAdd.ToArray());

        if (!overwright)
        {
            await CommandPermsCat(commandName, overwright, allow, "");
            return;
        }

        perms = Service.Cache.TryGetValue(ctx.Guild.Id, out permCache)
            ? permCache.Permissions.Source.ToList()
            : Permissionv2.GetDefaultPermlist;

        for (i = 0; i < needAdd.Count(); i++)
            await Service.UnsafeMovePerm(ctx.Guild.Id, perms.Last().Index, 1);
        await CommandPermsCat(commandName, overwright, allow, "");
    }

    public async Task<IUser?> TryGetUser(ulong id)
    {
        try
        {
            return await Context.Client.GetUserAsync(id);
        }
        catch
        {
            return null;
        }
    }

    public IRole? TryGetRole(ulong id) => Context.Guild.Roles.FirstOrDefault(x => x.Id == id);

    public async Task<IChannel?> TryGetChannel(ulong id)
    {
        try
        {
            return (IChannel)await Context.Client.GetChannelAsync(id);
        }
        catch
        {
            return null;
        }
    }

    public IEmote GetChannelEmote(IChannel channel)
    {
        return channel switch
        {
            ICategoryChannel => GetText("not_an_easter_egg").ToIEmote(),
            IForumChannel => "<:ForumChannelIcon:1086869270312517632>".ToIEmote(),
            INewsChannel => "<:ChannelAnnouncements:779042577114202122>".ToIEmote(),
            IThreadChannel => "<:threadchannel:824240882697633812>".ToIEmote(),
            IStageChannel => "<:stagechannel:824240882793447444>".ToIEmote(),
            IVoiceChannel => "<:ChannelVC:779036156607332394>".ToIEmote(),
            _ => "<:ChannelText:779036156175188001>".ToIEmote()
        };
    }
}