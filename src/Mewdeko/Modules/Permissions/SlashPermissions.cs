using System.Threading.Tasks;
using Discord.Commands;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using ContextType = Discord.Interactions.ContextType;

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

    public SlashPermissions(DbService db, InteractiveService inter, GuildSettingsService guildSettings)
    {
        interactivity = inter;
        this.guildSettings = guildSettings;
        this.db = db;
    }

    [SlashCommand("resetperms", "Reset Command Permissions"), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator)]
    public async Task ResetPerms()
    {
        await Service.Reset(ctx.Guild.Id).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("perms_reset").ConfigureAwait(false);
    }

    [SlashCommand("verbose", "Enables or Disables command errors"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task Verbose(PermissionSlash? action = null)
    {
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var config = await uow.GcWithPermissionsv2For(ctx.Guild.Id);
            config.VerbosePermissions = Convert.ToBoolean((int)action);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);
        }

        if (action == PermissionSlash.Allow)
            await ReplyConfirmLocalizedAsync("verbose_true").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("verbose_false").ConfigureAwait(false);
    }

    [SlashCommand("permrole", "Sets a role to change command permissions without admin"), Discord.Interactions.RequireContext(ContextType.Guild),
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

    [SlashCommand("listperms", "List currently set permissions"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
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
        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithDescription(string.Join("\n",
                perms.Skip(page * 10).Take(10).Select(p =>
                {
                    var str = $"`{p.Index + 1}.` {Format.Bold(p.GetCommand(guildSettings.GetPrefix(ctx.Guild).GetAwaiter().GetResult(), (SocketGuild)ctx.Guild))}";
                    if (p.Index == 0)
                        str += $" [{GetText("uneditable")}]";
                    return str;
                }))).WithTitle(Format.Bold(GetText("page", page + 1))).WithOkColor();
        }
    }

    [SlashCommand("removeperm", "Remove a permission based on its number"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task RemovePerm([Discord.Interactions.Summary("permission", "the permission to modify"), Autocomplete(typeof(PermissionAutoCompleter))] string perm)
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
                Format.Code(p.GetCommand(await guildSettings.GetPrefix(ctx.Guild), (SocketGuild)ctx.Guild))).ConfigureAwait(false);
        }
        catch (IndexOutOfRangeException)
        {
            await ReplyErrorLocalizedAsync("perm_out_of_range").ConfigureAwait(false);
        }
    }

    [SlashCommand("servercommand", "Enable or disable a command in the server"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task ServerCmd([Discord.Interactions.Summary("command", "the command to set permissions on"), Autocomplete(typeof(GenericCommandAutocompleter))] string command,
        PermissionSlash action)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action),
            IsCustomCommand = false
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

    [SlashCommand("servermodule", "Enable or disable a Module in the server"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task SrvrMdl([Discord.Interactions.Summary("module", "the module to set permissions on"), Autocomplete(typeof(ModuleAutoCompleter))] string module,
        PermissionSlash action)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action)
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

    [SlashCommand("usercommand", "Enable or disable a command for a user"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task UsrCmd([Discord.Interactions.Summary("command", "the command to set permissions on"), Autocomplete(typeof(GenericCommandAutocompleter))] string command,
        PermissionSlash action, IGuildUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action),
            IsCustomCommand = false
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

    [SlashCommand("usermodule", "Enable or disable a module for a user"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task UsrMdl([Discord.Interactions.Summary("module", "the module to set permissions on"), Autocomplete(typeof(ModuleAutoCompleter))] string module,
        PermissionSlash action, IGuildUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action)
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

    [SlashCommand("rolecommand", "Enable or disable a command for a role"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task RoleCmd([Discord.Interactions.Summary("command", "the command to set permissions on"), Autocomplete(typeof(GenericCommandAutocompleter))] string command,
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
            State = Convert.ToBoolean((int)action),
            IsCustomCommand = false
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

    [SlashCommand("rolemodule", "Enable or disable a module for a role"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task RoleMdl([Discord.Interactions.Summary("module", "the module to set permissions on"), Autocomplete(typeof(ModuleAutoCompleter))] string module,
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
            State = Convert.ToBoolean((int)action)
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

    [SlashCommand("channelcommand", "Enable or disable a command for a channel"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task ChnlCmd([Discord.Interactions.Summary("command", "the command to set permissions on"), Autocomplete(typeof(GenericCommandAutocompleter))] string command,
        PermissionSlash action, ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action),
            IsCustomCommand = false
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

    [SlashCommand("channelmodule", "Enable or disable a module for a channel"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task ChnlMdl([Discord.Interactions.Summary("module", "the module to set permissions on"), Autocomplete(typeof(ModuleAutoCompleter))] string module,
        PermissionSlash action, ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action)
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

    [SlashCommand("allchannelmodules", "Enable or disable all modules in a channel"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task AllChnlMdls(PermissionSlash action, ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = Convert.ToBoolean((int)action)
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

    [SlashCommand("categorycommand", "Enable or disable commands for a category"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task CatCmd([Discord.Interactions.Summary("command", "the command to set permissions on"), Autocomplete(typeof(GenericCommandAutocompleter))] string command,
        PermissionSlash action, ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action),
            IsCustomCommand = false
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

    [SlashCommand("categorymodule", "Enable or disable a module for a category"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task CatMdl([Discord.Interactions.Summary("module", "the module to set permissions on"), Autocomplete(typeof(ModuleAutoCompleter))] string module,
        PermissionSlash action, ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = Convert.ToBoolean((int)action)
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

    [SlashCommand("allcategorymodules", "Enable or disable all modules in a category"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task AllCatMdls(PermissionSlash action, ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = Convert.ToBoolean((int)action)
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

    [SlashCommand("allrolemodules", "Enable or disable all modules for a role"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
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
            State = Convert.ToBoolean((int)action)
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

    [SlashCommand("allusermodules", "Enable or disable all modules for a user"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task AllUsrMdls(PermissionSlash action, IUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = Convert.ToBoolean((int)action)
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

    [SlashCommand("allservermodules", "Enable or disable all modules in the server"), Discord.Interactions.RequireContext(ContextType.Guild), PermRoleCheck]
    public async Task AllSrvrMdls(PermissionSlash action)
    {
        var newPerm = new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = Convert.ToBoolean((int)action)
        };

        var allowUser = new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = ctx.User.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = true
        };

        await Service.AddPermissions(ctx.Guild.Id,
            newPerm,
            allowUser).ConfigureAwait(false);

        if (Convert.ToBoolean((int)action))
            await ReplyConfirmLocalizedAsync("asm_enable").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("asm_disable").ConfigureAwait(false);
    }
}