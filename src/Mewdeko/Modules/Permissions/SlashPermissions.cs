#undef FORCE_ADD_DUMMY_PERMS

using Discord.Commands;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Swan;
using ContextType = Discord.Interactions.ContextType;
using TextUserPermAttribute = Mewdeko.Common.Attributes.TextCommands.UserPermAttribute;

namespace Mewdeko.Modules.Permissions;

/// <summary>
///     The permissions slash commands.
/// </summary>
[Discord.Interactions.Group("permissions", "Change or view command permissions.")]
public class SlashPermissions : MewdekoSlashModuleBase<PermissionService>
{
    /// <summary>
    ///     Enum for permission control.
    /// </summary>
    public enum PermissionSlash
    {
        /// <summary>
        ///     Grants permission.
        /// </summary>
        Allow = 1,

        /// <summary>
        ///     Revokes permission.
        /// </summary>
        Deny = 0
    }

    /// <summary>
    ///     Enum for indicating a reset operation.
    /// </summary>
    public enum Reset
    {
        /// <summary>
        ///     Specifies a reset.
        /// </summary>
        Reset
    }

    private readonly CommandService cmdServe;


    private readonly DbContextProvider dbProvider;
    private readonly DiscordPermOverrideService dpoS;
    private readonly GuildSettingsService guildSettings;
    private readonly InteractiveService interactivity;

    /// <summary>
    ///     Initializes a new instance of the SlashPermissions class.
    /// </summary>
    /// <param name="db">Database service instance for database operations.</param>
    /// <param name="inter">Interactive service for managing interactive commands.</param>
    /// <param name="guildSettings">Service for accessing and modifying guild settings.</param>
    /// <param name="dpoS">Discord permissions override service for custom permission handling.</param>
    /// <param name="cmdServe">Command service for Discord bot commands management.</param>
    /// <remarks>
    ///     This constructor is responsible for setting up the necessary services required for
    ///     managing slash command permissions, interactive commands, guild settings, and command execution.
    ///     Each service parameter provided plays a crucial role in the operation and customization
    ///     of the bot's functionality, especially in the context of permissions and settings management.
    /// </remarks>
    public SlashPermissions(DbContextProvider dbProvider, InteractiveService inter, GuildSettingsService guildSettings,
        DiscordPermOverrideService dpoS, CommandService cmdServe)
    {
        interactivity = inter;
        this.guildSettings = guildSettings;
        this.dbProvider = dbProvider;
        this.dpoS = dpoS;
        this.cmdServe = cmdServe;
    }

    /// <summary>
    ///     Resets command permissions for the guild.
    /// </summary>
    /// <remarks>
    ///     This slash command resets all custom command permissions back to their default states within the guild.
    ///     Requires the user to have Administrator permissions to execute.
    ///     After resetting permissions, a confirmation message is sent to the command invoker.
    /// </remarks>
    [SlashCommand("resetperms", "Reset Command Permissions")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ResetPerms()
    {
        await Service.Reset(ctx.Guild.Id).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("perms_reset").ConfigureAwait(false);
    }

    /// <summary>
    ///     Enables or disables verbose command error messages.
    /// </summary>
    /// <param name="action">Optional parameter to enable (Allow) or disable (Deny) verbose permissions.</param>
    /// <remarks>
    ///     This command toggles the verbosity of command error messages, providing detailed feedback when enabled.
    ///     It requires a specific role checked by PermRoleCheck to execute. If no action is specified, the command defaults to
    ///     disabling verbose errors.
    /// </remarks>
    [SlashCommand("verbose", "Enables or Disables command errors")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task Verbose(PermissionSlash? action = null)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        {
            var config = await dbContext.GcWithPermissionsv2For(ctx.Guild.Id);
            config.VerbosePermissions = action.Value.ToBoolean();
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);
        }

        if (action == PermissionSlash.Allow)
            await ReplyConfirmLocalizedAsync("verbose_true").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("verbose_false").ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets or resets a role that can change command permissions without requiring admin rights.
    /// </summary>
    /// <param name="role">The role to set as the permission role. If null, resets the permission role.</param>
    /// <remarks>
    ///     This command allows for setting a specific role to manage command permissions, providing a way to delegate
    ///     permissions management without granting full Administrator rights.
    ///     If the command is invoked without specifying a role, or if the @everyone role is selected, it will reset the
    ///     permission role to its default state.
    ///     Requires Administrator permissions to execute. Confirmation is sent upon changing the permission role.
    /// </remarks>
    [SlashCommand("permrole", "Sets a role to change command permissions without admin")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [Priority(0)]
    public async Task PermRole(IRole? role = null)
    {
        if (role != null && role == role.Guild.EveryoneRole)
            return;


        await using var dbContext = await dbProvider.GetContextAsync();

        if (role == null)
        {
            var config = await dbContext.GcWithPermissionsv2For(ctx.Guild.Id);
            config.PermissionRole = 0.ToString();
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);
            await ReplyConfirmLocalizedAsync("permrole_reset").ConfigureAwait(false);
        }
        else
        {
            var config = await dbContext.GcWithPermissionsv2For(ctx.Guild.Id);
            config.PermissionRole = role.Id.ToString();
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);

            await ReplyConfirmLocalizedAsync("permrole_changed", Format.Bold(role.Name)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Lists currently set permissions for commands in the guild.
    /// </summary>
    /// <remarks>
    ///     This command fetches and displays a list of all custom command permissions that have been configured in the guild.
    ///     It presents the permissions in a paginated format, allowing users to navigate through the list.
    ///     The command checks for permission roles before execution. If no custom permissions are set, it will display the
    ///     default permissions list.
    /// </remarks>
    [SlashCommand("listperms", "List currently set permissions")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
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

    /// <summary>
    ///     Removes a specified permission based on its list number.
    /// </summary>
    /// <param name="perm">The number of the permission to remove, as displayed in the listperms command.</param>
    /// <remarks>
    ///     This command allows for the removal of a specific command permission by its number.
    ///     The command validates the provided number and ensures that the default permission (index 0) cannot be removed.
    ///     Upon successful removal, a confirmation message is displayed. If the specified permission number does not exist, an
    ///     error message is shown.
    ///     Requires permission role check for execution.
    /// </remarks>
    [SlashCommand("removeperm", "Remove a permission based on its number")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task RemovePerm(
        [Discord.Interactions.Summary("permission", "the permission to modify")]
        [Autocomplete(typeof(PermissionAutoCompleter))]
        string perm)
    {
        var index = int.Parse(perm);
        if (index == 0)
        {
            await ctx.Interaction.SendErrorAsync("You cannot remove this permission!", Config).ConfigureAwait(false);
            return;
        }

        try
        {
            await using var dbContext = await dbProvider.GetContextAsync();
            var config = await dbContext.GcWithPermissionsv2For(ctx.Guild.Id);
            var permsCol = new PermissionsCollection<Permissionv2>(config.Permissions);
            var p = permsCol[index];
            permsCol.RemoveAt(index);
            dbContext.Remove(p);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);

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

    /// <summary>
    ///     Enables or disables a specific command server-wide.
    /// </summary>
    /// <param name="command">The command to set permissions on.</param>
    /// <param name="action">The action to apply, either Allow (enable) or Deny (disable).</param>
    /// <remarks>
    ///     This command changes the permission state of a specified command across the entire server.
    ///     It allows for granular control over command availability, enhancing server customization and security.
    ///     The change is immediate, affecting all users within the server based on the specified action.
    ///     Requires the executing user to have a role with permission management capabilities.
    /// </remarks>
    [SlashCommand("servercommand", "Enable or disable a command in the server")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task ServerCmd(
        [Discord.Interactions.Summary("command", "the command to set permissions on")]
        [Autocomplete(typeof(GenericCommandAutocompleter))]
        string command,
        PermissionSlash action)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = action.ToBoolean(),
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

    /// <summary>
    ///     Enables or disables a module server-wide.
    /// </summary>
    /// <param name="module">The module to set permissions on.</param>
    /// <param name="action">The action to apply, either Allow (enable) or Deny (disable).</param>
    /// <remarks>
    ///     This command allows administrators to manage the availability of entire modules within their server,
    ///     enabling or disabling sets of functionality in one action. It's particularly useful for customizing the server
    ///     experience
    ///     and managing access to groups of commands.
    ///     Execution requires the user to have a role designated for permission management.
    /// </remarks>
    [SlashCommand("servermodule", "Enable or disable a Module in the server")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task SrvrMdl(
        [Discord.Interactions.Summary("module", "the module to set permissions on")]
        [Autocomplete(typeof(ModuleAutoCompleter))]
        string module,
        PermissionSlash action)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = action.ToBoolean()
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

    /// <summary>
    ///     Enables or disables a command for a specific user in the guild.
    /// </summary>
    /// <param name="command">The command to set permissions on.</param>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) the command.</param>
    /// <param name="user">The user for whom the command permission will be set.</param>
    /// <remarks>
    ///     This command modifies the accessibility of a specified command for an individual user,
    ///     allowing for precise control over command usage. Useful for granting or restricting command access on a per-user
    ///     basis.
    /// </remarks>
    [SlashCommand("usercommand", "Enable or disable a command for a user")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task UsrCmd(
        [Discord.Interactions.Summary("command", "the command to set permissions on")]
        [Autocomplete(typeof(GenericCommandAutocompleter))]
        string command,
        PermissionSlash action, IGuildUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = action.ToBoolean(),
            IsCustomCommand = true
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

    /// <summary>
    ///     Enables or disables a module for a specific user in the guild.
    /// </summary>
    /// <param name="module">The module to set permissions on.</param>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) the module.</param>
    /// <param name="user">The user for whom the module permission will be set.</param>
    /// <remarks>
    ///     Similar to command permissions, this allows fine-grained control over module access for individual users,
    ///     enhancing the customization of user experiences within the server.
    /// </remarks>
    [SlashCommand("usermodule", "Enable or disable a module for a user")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task UsrMdl(
        [Discord.Interactions.Summary("module", "the module to set permissions on")]
        [Autocomplete(typeof(ModuleAutoCompleter))]
        string module,
        PermissionSlash action, IGuildUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = action.ToBoolean()
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

    /// <summary>
    ///     Enables or disables a command for a specific role in the guild.
    /// </summary>
    /// <param name="command">The command to set permissions on.</param>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) the command.</param>
    /// <param name="role">The role for which the command permission will be set.</param>
    /// <remarks>
    ///     This command facilitates role-based command permission management, allowing or disallowing command use for entire
    ///     groups of users.
    ///     It cannot be applied to the @everyone role, ensuring that some level of command access remains universal.
    /// </remarks>
    [SlashCommand("rolecommand", "Enable or disable a command for a role")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task RoleCmd(
        [Discord.Interactions.Summary("command", "the command to set permissions on")]
        [Autocomplete(typeof(GenericCommandAutocompleter))]
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
            State = action.ToBoolean(),
            IsCustomCommand = true
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

    /// <summary>
    ///     Enables or disables a module for a specific role within the guild.
    /// </summary>
    /// <param name="module">The name of the module to set permissions for.</param>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) the module for the specified role.</param>
    /// <param name="role">
    ///     The role for which the module permission will be set. The @everyone role is exempt from this
    ///     command.
    /// </param>
    /// <remarks>
    ///     This command offers a nuanced control over module accessibility on a role-by-role basis, enhancing the server's
    ///     ability to customize module usage.
    ///     Using this command, server administrators can tailor the bot's functionality to suit the needs and privileges of
    ///     different user groups within their community.
    /// </remarks>
    [SlashCommand("rolemodule", "Enable or disable a module for a role")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task RoleMdl(
        [Discord.Interactions.Summary("module", "the module to set permissions on")]
        [Autocomplete(typeof(ModuleAutoCompleter))]
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
            State = action.ToBoolean()
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

    /// <summary>
    ///     Enables or disables a command for a specific channel in the guild.
    /// </summary>
    /// <param name="command">The command to set permissions on.</param>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) the command in the specified channel.</param>
    /// <param name="chnl">The channel for which the command permission will be set.</param>
    /// <remarks>
    ///     This command allows server administrators to control the availability of specific commands within individual
    ///     channels,
    ///     providing a way to customize the bot's interaction based on the channel's context or purpose. This ensures that
    ///     commands are only used where they are most appropriate.
    /// </remarks>
    [SlashCommand("channelcommand", "Enable or disable a command for a channel")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task ChnlCmd(
        [Discord.Interactions.Summary("command", "the command to set permissions on")]
        [Autocomplete(typeof(GenericCommandAutocompleter))]
        string command,
        PermissionSlash action, ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = action.ToBoolean(),
            IsCustomCommand = true
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

    /// <summary>
    ///     Enables or disables a module for a specific channel within the guild.
    /// </summary>
    /// <param name="module">The module to set permissions on.</param>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) the module in the specified channel.</param>
    /// <param name="chnl">The channel for which the module permission will be set.</param>
    /// <remarks>
    ///     By controlling module access at the channel level, this command provides granular control over where certain
    ///     functionalities of the bot can be accessed within the guild.
    ///     It's useful for restricting module usage to channels dedicated to specific topics or activities, thereby keeping
    ///     the server organized and focused.
    /// </remarks>
    [SlashCommand("channelmodule", "Enable or disable a module for a channel")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task ChnlMdl(
        [Discord.Interactions.Summary("module", "the module to set permissions on")]
        [Autocomplete(typeof(ModuleAutoCompleter))]
        string module,
        PermissionSlash action, ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = action.ToBoolean()
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

    /// <summary>
    ///     Enables or disables all modules for a specified channel within the guild.
    /// </summary>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) all modules in the channel.</param>
    /// <param name="chnl">The text channel for which all module permissions will be set.</param>
    /// <remarks>
    ///     This command provides a quick way to modify access to all bot modules for a particular channel,
    ///     either granting full access or restricting it entirely. Useful for configuring channels with specific purposes
    ///     without adjusting permissions for each module individually.
    /// </remarks>
    [SlashCommand("allchannelmodules", "Enable or disable all modules in a channel")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task AllChnlMdls(PermissionSlash action, ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.ToBoolean()
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

    /// <summary>
    ///     Enables or disables commands for a specific category within the guild.
    /// </summary>
    /// <param name="command">The command to set permissions on.</param>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) the command for the category.</param>
    /// <param name="chnl">The category channel for which the command permission will be set.</param>
    /// <remarks>
    ///     Managing command permissions at the category level allows for uniform command availability across all channels
    ///     within that category,
    ///     streamlining the permission management process for large guilds.
    /// </remarks>
    [SlashCommand("categorycommand", "Enable or disable commands for a category")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task CatCmd(
        [Discord.Interactions.Summary("command", "the command to set permissions on")]
        [Autocomplete(typeof(GenericCommandAutocompleter))]
        string command,
        PermissionSlash action, ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.ToLowerInvariant(),
            State = action.ToBoolean(),
            IsCustomCommand = true
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

    /// <summary>
    ///     Enables or disables a module for a specific category within the guild.
    /// </summary>
    /// <param name="module">The module to set permissions on.</param>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) the module for the specified category.</param>
    /// <param name="chnl">The category channel for which the module permission will be set.</param>
    /// <remarks>
    ///     This command allows for nuanced control over which modules are accessible in channels within a specific category,
    ///     facilitating tailored functionality across different parts of a guild.
    /// </remarks>
    [SlashCommand("categorymodule", "Enable or disable a module for a category")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task CatMdl(
        [Discord.Interactions.Summary("module", "the module to set permissions on")]
        [Autocomplete(typeof(ModuleAutoCompleter))]
        string module,
        PermissionSlash action, ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.ToLowerInvariant(),
            State = action.ToBoolean()
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

    /// <summary>
    ///     Enables or disables all modules within a specific category.
    /// </summary>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) all modules for the category.</param>
    /// <param name="chnl">The category channel for which all module permissions will be set.</param>
    /// <remarks>
    ///     Offers a convenient way to quickly enable or disable all bot functionality within a category,
    ///     useful for managing areas of the guild dedicated to specific types of interactions.
    /// </remarks>
    [SlashCommand("allcategorymodules", "Enable or disable all modules in a category")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task AllCatMdls(PermissionSlash action, ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.ToBoolean()
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

    /// <summary>
    ///     Enables or disables all modules for a specific role in the guild.
    /// </summary>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) all modules for the role.</param>
    /// <param name="role">The role for which all module permissions will be set. The @everyone role is excluded.</param>
    /// <remarks>
    ///     This command provides a straightforward approach to control the access level of entire groups of users
    ///     by adjusting their role's permissions regarding the bot's modules, streamlining the permission management process.
    /// </remarks>
    [SlashCommand("allrolemodules", "Enable or disable all modules for a role")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
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
            State = action.ToBoolean()
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

    /// <summary>
    ///     Enables or disables all modules for a specific user within the guild.
    /// </summary>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) all modules for the user.</param>
    /// <param name="user">The user for whom all module permissions will be set.</param>
    /// <remarks>
    ///     This command is especially useful for quickly adjusting a single user's access to the bot's functionalities,
    ///     allowing for personalized control over the bot's interaction with individual members.
    /// </remarks>
    [SlashCommand("allusermodules", "Enable or disable all modules for a user")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task AllUsrMdls(PermissionSlash action, IUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.ToBoolean()
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

    /// <summary>
    ///     Enables or disables all modules for the entire server.
    /// </summary>
    /// <param name="action">Specifies whether to enable (Allow) or disable (Deny) all modules server-wide.</param>
    /// <remarks>
    ///     This command allows administrators to quickly toggle the availability of all bot modules within the server,
    ///     effectively turning all bot functionalities on or off. This is particularly useful for managing bot permissions
    ///     during specific server events or maintenance periods.
    /// </remarks>
    [SlashCommand("allservermodules", "Enable or disable all modules in the server")]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    [PermRoleCheck]
    public async Task AllSrvrMdls(PermissionSlash action)
    {
        var newPerm = new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.ToBoolean()
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

    /// <summary>
    ///     Updates the permission menu message for a command.
    /// </summary>
    /// <param name="commandName">The name of the command to update the permission menu for.</param>
    /// <remarks>
    ///     This method is triggered via a component interaction. It updates the permission menu message to reflect
    ///     current permissions or to offer quick adjustments for the specified command's permissions.
    /// </remarks>
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
                Emote.Parse("<:IconSettings:1290522076486041714>"), disabled: true)
            .WithButton(GetText("back"), $"help_component_restore.{commandName}",
                emote: "<:perms_back_arrow:1290522013861023848>".ToIEmote());

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

        if (effecting.Any(x => x.PrimaryTarget == PrimaryPermissionType.Server && !x.State))
            cb.WithButton(GetText("perm_quick_options_disable_disabled"), $"command_toggle_disable.{commandName}",
                ButtonStyle.Success,
                "<:perms_check:1290520193839140884>".ToIEmote());
        else
            cb.WithButton(GetText("perm_quick_options_disable_enabled"), $"command_toggle_disable.{commandName}",
                ButtonStyle.Danger,
                "<:perms_disabled:1290520276479643698>".ToIEmote());

        if (effecting.Any() || dpoUsed)
            cb.WithButton(GetText("local_perms_reset"), $"local_perms_reset.{commandName}", ButtonStyle.Danger,
                "<:perms_warning:1290520381303820372>".ToIEmote());

        cb.WithSelectMenu($"cmd_perm_spawner.{commandName}", [
            new SelectMenuOptionBuilder(GetText("cmd_perm_spawner_required_perms"), "dpo",
                GetText("cmd_perm_spawner_required_perms_desc"),
                "<:perms_dpo:1290520438706802698>".ToIEmote()),

            new SelectMenuOptionBuilder(GetText("cmd_perm_spawner_user_perms"), "usr",
                GetText("cmd_perm_spawner_user_perms_desc"),
                "<:perms_user_perms:1290520494747029600>".ToIEmote()),

            new SelectMenuOptionBuilder(GetText("cmd_perm_spawner_role_perms"), "rol",
                GetText("cmd_perm_spawner_role_perms_desc"),
                "<:role:1290520559163019304>".ToIEmote()),

            new SelectMenuOptionBuilder(GetText("cmd_perm_spawner_channel_perms"), "chn",
                GetText("cmd_perm_spawner_channel_perms_desc"),
                "<:ChannelText:1290520630109798420>".ToIEmote()),

            new SelectMenuOptionBuilder(GetText("cmd_perm_spawner_category_perms"), "cat",
                GetText("cmd_perm_spawner_category_perms_desc"),
                GetText("not_an_easter_egg").ToIEmote())
        ], GetText("advanced_options"));

        await RespondAsync(components: cb.Build(), embeds: quickEmbeds, ephemeral: true);
    }

    /// <summary>
    ///     Clears redundant permissions for a specified command.
    /// </summary>
    /// <param name="commandName">The name of the command to clear redundant permissions for.</param>
    /// <remarks>
    ///     Identifies and removes redundant permission entries for a command across all permission targets (e.g., roles,
    ///     users, channels).
    ///     This helps maintain a clean and efficient permissions setup by eliminating unnecessary or conflicting permission
    ///     entries.
    /// </remarks>
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
                $"credperms_m.{(int)perm.PrimaryTarget}.{perm.PrimaryTargetId}.{(int)perm.SecondaryTarget}.{perm.SecondaryTargetName}",
                [
                    new SelectMenuOptionBuilder(GetText("perm_quick_options_redundant_tool_enable"), "enabled",
                        GetText("perm_quick_options_redundant_tool_enabled_description")),

                    new SelectMenuOptionBuilder(GetText("perm_quick_options_redundant_tool_disable"), "disabled",
                        GetText("perm_quick_options_redundant_tool_disable_description")),

                    new SelectMenuOptionBuilder(GetText("perm_quick_options_redundant_tool_clear"), "clear",
                        GetText("perm_quick_options_redundant_tool_clear_description")),

                    new SelectMenuOptionBuilder(GetText("perm_quick_options_redundant_tool_current"), "current",
                        GetText("perm_quick_options_redundant_tool_current_description"))
                ], "Action");

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

    /// <summary>
    ///     Resolves permission configurations through a menu-based interface.
    /// </summary>
    /// <param name="primaryTargetType">The type of the primary permission target.</param>
    /// <param name="primaryTargetIdRaw">The ID of the primary permission target.</param>
    /// <param name="secondaryTargetType">The type of the secondary permission target.</param>
    /// <param name="secondaryTargetId">The ID or name of the secondary permission target.</param>
    /// <remarks>
    ///     Allows administrators to resolve permission configurations, such as enabling, disabling, or clearing permissions,
    ///     for a given command or module through a user-friendly menu interface. This method simplifies permission management
    ///     by providing actionable options in response to identified permission issues or requirements.
    /// </remarks>
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

    /// <summary>
    ///     Toggles the disabled state of a command server-wide.
    /// </summary>
    /// <param name="commandName">The name of the command to toggle the disabled state for.</param>
    /// <remarks>
    ///     Enables or disables a command for the entire server. This command is particularly useful for quickly
    ///     disabling commands that may be causing issues or that need to be temporarily restricted server-wide.
    /// </remarks>
    [ComponentInteraction("command_toggle_disable.*", true)]
    [Discord.Interactions.RequireUserPermission(GuildPermission.Administrator)]
    [Discord.Interactions.RequireContext(ContextType.Guild)]
    public async Task ToggleCommanddisabled(string commandName)
    {
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

        if (sc is not null && sc.State)
        {
            await Service.RemovePerm(ctx.Guild.Id, sc.Index);
            sc = null;
        }

        if (sc is null)
        {
            await using var dbContext = await dbProvider.GetContextAsync();

            await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
            {
                GuildConfigId = dbContext.ForGuildId(ctx.Guild.Id).Id,
                IsCustomCommand = true,
                PrimaryTarget = PrimaryPermissionType.Server,
                PrimaryTargetId = 0,
                SecondaryTarget = SecondaryPermissionType.Command,
                SecondaryTargetName = commandName,
                State = false
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

    /// <summary>
    ///     Resets local permissions for a specific command within the guild.
    /// </summary>
    /// <param name="commandName">The name of the command to reset permissions for.</param>
    /// <remarks>
    ///     This method removes all custom permissions set for a given command within the guild,
    ///     reverting them back to the default or global permission settings. It's particularly useful
    ///     for administrators to quickly normalize command permissions across the server.
    /// </remarks>
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

    /// <summary>
    ///     Initiates a permission configuration process for a specific command.
    /// </summary>
    /// <param name="commandName">The command name for which to spawn permission configurations.</param>
    /// <param name="values">The actions or types of permissions to configure for the command.</param>
    /// <remarks>
    ///     This method serves as an entry point for configuring detailed permissions for a command,
    ///     allowing for the selection among different permission types like direct user permissions, role-based permissions,
    ///     channel-specific permissions, etc. It facilitates granular control over who can use the command.
    /// </remarks>
    [ComponentInteraction("cmd_perm_spawner.*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermSpawner(string commandName, string[] values)
    {
        return values.First() switch
        {
            "dpo" => CommandPermsDpo(commandName),
            "usr" => CommandPermsUsr(commandName, true, true, ""),
            "rol" => CommandPermsRol(commandName, true, true, ""),
            "chn" => CommandPermsChn(commandName, true, true, ""),
            "cat" => CommandPermsCat(commandName, true, true, ""),
            _ => UpdateMessageWithPermenu(commandName)
        };
    }

    /// <summary>
    ///     Configures permissions for a command based on Discord's built-in permissions.
    /// </summary>
    /// <param name="commandName">The name of the command for which to set permissions.</param>
    /// <remarks>
    ///     Allows setting permissions for a command based on Discord's predefined permissions,
    ///     offering a way to quickly align command access with Discord's role permissions.
    ///     This method aids in maintaining a consistent permission structure across the server.
    /// </remarks>
    [ComponentInteraction("cmd_perm_spawner_dpo.*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermsDpo(string commandName)
    {
        var perms = Enum.GetValues<GuildPermission>();
        List<SelectMenuBuilder> selects = [];

        dpoS.TryGetOverrides(ctx.Guild.Id, commandName, out var effecting);

        var info = cmdServe.Commands.First(x => x.Name == commandName);
        var userPerm = ((TextUserPermAttribute)info.Preconditions.FirstOrDefault(ca => ca is TextUserPermAttribute))
            ?.UserPermissionAttribute.GuildPermission;

        var basePerms = userPerm is not null
            ? perms.Where(x => (userPerm & x) == x).ToList()
            : [];
        var truePerms = perms.Where(x => (effecting & x) == x);

        if (!truePerms.Any())
            truePerms = basePerms;
        for (var i = 0; i < 25 && (selects.Count - 1) * 25 < perms.Length && selects.Count <= 5; i++)
        {
            selects.Add(new SelectMenuBuilder()
                .WithCustomId($"update_cmd_dpo.{commandName}${i}")
                .WithMinValues(0)
                .WithPlaceholder(GetText("cmd_perm_spawner_dpo_page", selects.Count + 1)));
            var current = selects.Last();
            for (var j = 0; j < 25 && (selects.Count - 1) * 25 + j < perms.Length; j++)
            {
                var cdat = perms[(selects.Count - 1) * 25 + j];
                current.AddOption(cdat.ToString(), ((ulong)cdat).ToString(), cdat.ToString(),
                    isDefault: truePerms.Any(x => x == cdat));
                current.MaxValues = j + 1;
            }
        }

        var cb = new ComponentBuilder()
            .WithRows(selects.Where(x => x.Options.Count > 0).Select(x => new ActionRowBuilder().WithSelectMenu(x)))
            .WithButton(GetText("back"), $"permenu_update.{commandName}",
                emote: "<:perms_back_arrow:1290522013861023848>".ToIEmote());

        return (ctx.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }

    /// <summary>
    ///     Updates Discord's built-in permissions for a specific command.
    /// </summary>
    /// <param name="commandName">The command name for which permissions are being updated.</param>
    /// <param name="index">The index of the permission set to update.</param>
    /// <param name="values">The new permission values to apply.</param>
    /// <remarks>
    ///     This method applies the selected Discord permissions to the specified command,
    ///     allowing server administrators to customize which built-in permissions are required to use the command.
    /// </remarks>
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
            : [];
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

    /// <summary>
    ///     Configures user-specific permissions for a command.
    /// </summary>
    /// <param name="commandName">The name of the command to configure.</param>
    /// <param name="overwrite">Indicates if the permission should overwrite existing permissions.</param>
    /// <param name="allow">Determines if the permission allows or denies command access.</param>
    /// <param name="_">A placeholder parameter, currently not used.</param>
    /// <remarks>
    ///     This method manages user-specific permissions for a command,
    ///     enabling detailed access control on a per-user basis.
    /// </remarks>
    [ComponentInteraction("command_perm_spawner_usr.*.*.*$*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermsUsr(string commandName, bool overwrite, bool allow, string _)
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
        // get perm overwrites targeting users
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.User)
            .Where(x => x.State)
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
            .WithButton(GetText("perm_quick_options_overwrite"),
                $"command_perm_spawner_usr.{commandName}.{true}.{allow}$1",
                overwrite ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_overwrite:1085421377798029393>".ToIEmote(), disabled: overwrite)
            .WithButton(GetText("perm_quick_options_fallback"),
                $"command_perm_spawner_usr.{commandName}.{false}.{allow}$2",
                !overwrite ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_fallback:1085421376032231444>".ToIEmote(), disabled: !overwrite)
            .WithButton(GetText("perm_quick_options_allow"),
                $"command_perm_spawner_usr.{commandName}.{overwrite}.{true}$3",
                allow ? ButtonStyle.Success : ButtonStyle.Secondary,
                "<:perms_check:1085356998247317514>".ToIEmote(), disabled: allow)
            .WithButton(GetText("perm_quick_options_deny"),
                $"command_perm_spawner_usr.{commandName}.{overwrite}.{false}$4",
                !allow ? ButtonStyle.Danger : ButtonStyle.Secondary,
                "<:perms_disabled:1085358511900327956>".ToIEmote(), disabled: !allow);

        var i = 0;
        for (i = 0; i < Math.Min(splitGroups.Count, 3); i++)
        {
            var options = splitGroups[i]
                .Select(async x => (x, user: await TryGetUser(x.PrimaryTargetId)))
                .Select(x => x.Result)
                .Select(x => new SelectMenuOptionBuilder(x.user?.ToString() ?? "Unknown#0000", x.x.Id.ToString(),
                    GetText($"perms_quick_options_user_remove_{(allow ? "allow" : "deny")}", x.x.PrimaryTargetId),
                    "<:perms_user_perms:1085426466818359367>".ToIEmote(), true));
            var sb = new SelectMenuBuilder($"perm_quick_options_user_remove.{commandName}.{overwrite}.{allow}${i}",
                options.ToList(), GetText("perms_quick_options_user_remove"),
                options.Count(), 0);
            cb.WithSelectMenu(sb);
        }

        cb.WithSelectMenu(
            $"perm_quick_options_user_add.{commandName}.{overwrite}.{allow}${Random.Shared.NextInt64(i, long.MaxValue)}",
            placeholder: GetText("perm_quick_options_add_users"), minValues: 1, maxValues: 10,
            type: ComponentType.UserSelect, options: null);

        return (Context.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }

    /// <summary>
    ///     Removes user-specific overrides for a command.
    /// </summary>
    /// <param name="commandName">The name of the command to remove overrides for.</param>
    /// <param name="overwrite">Specifies whether the operation should overwrite existing permissions.</param>
    /// <param name="allow">Indicates if the override being removed is an allow or deny permission.</param>
    /// <param name="index">The index within the batch of permissions being processed.</param>
    /// <param name="values">The user IDs for which to remove the permission overrides.</param>
    /// <remarks>
    ///     Facilitates the removal of specific user permission overrides, cleaning up any custom configurations
    ///     that are no longer needed or desired for the given command.
    /// </remarks>
    [ComponentInteraction("perm_quick_options_user_remove.*.*.*$*", true)]
    public async Task RemoveUserOveride(string commandName, bool overwrite, bool allow, int index, string[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.User)
            .Where(x => x.State)
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
        foreach (var p in perms.Where(x => !values.Contains(x.Id.ToString())))
        {
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);
        }

        await CommandPermsUsr(commandName, overwrite, allow, "");
    }

    /// <summary>
    ///     Adds user-specific overrides for a command.
    /// </summary>
    /// <param name="commandName">The name of the command to add overrides for.</param>
    /// <param name="overwrite">Indicates whether to overwrite existing permissions.</param>
    /// <param name="allow">Determines if the override should allow or deny access to the command.</param>
    /// <param name="_">A placeholder parameter, currently not used.</param>
    /// <param name="values">The users for whom to add the permission overrides.</param>
    /// <remarks>
    ///     Enables the addition of permission overrides on a per-user basis for a command,
    ///     allowing for precise control over command access among server members.
    /// </remarks>
    [ComponentInteraction("perm_quick_options_user_add.*.*.*$*", true)]
    public async Task AddUserOveride(string commandName, bool overwrite, bool allow, string _, IUser[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        var matchingPerms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.User)
            .Where(x => x.State)
            .ToList();

        var needMod = values.Where(x => !matchingPerms.Any(y => y.PrimaryTargetId == x.Id));
        var needRems = perms.Where(x => needMod.Any(y => x.PrimaryTargetId == y.Id));
        var needAdd = needMod.Where(x => !needRems.Any(y => x.Id == y.PrimaryTargetId));

        var i = -1;
        foreach (var p in needRems)
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);

        var trueAdd = needAdd.Select(x => new Permissionv2
        {
            IsCustomCommand = true,
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = x.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = commandName,
            State = true
        });
        await Service.AddPermissions(ctx.Guild.Id, trueAdd.ToArray());

        if (!overwrite)
        {
            await CommandPermsUsr(commandName, overwrite, allow, "");
            return;
        }

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        for (i = 0; i < needAdd.Count(); i++)
            await Service.UnsafeMovePerm(ctx.Guild.Id, perms.Last().Index, 1);
        await CommandPermsUsr(commandName, overwrite, allow, "");
    }

    /// <summary>
    ///     Restores the help component for a specific command.
    /// </summary>
    /// <param name="commandName">The name of the command for which the help component is being restored.</param>
    /// <remarks>
    ///     This method dynamically generates interactive buttons for command help,
    ///     facilitating user access to command usage information and permission settings.
    /// </remarks>
    [ComponentInteraction("help_component_restore.*", true)]
    public Task HelpComponentRestore(string commandName)
    {
        var cb = new ComponentBuilder()
            .WithButton(GetText("help_run_cmd"), $"runcmd.{commandName}", ButtonStyle.Success)
            .WithButton(GetText("help_permenu_link"), $"permenu_update.{commandName}", ButtonStyle.Primary,
                Emote.Parse("<:IconPrivacySettings:845090111976636446>"));
        return (ctx.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }

    /// <summary>
    ///     Configures permissions for a command targeted at roles.
    /// </summary>
    /// <param name="commandName">The name of the command to configure permissions for.</param>
    /// <param name="overwrite">Indicates if permissions should overwrite existing settings.</param>
    /// <param name="allow">Determines if the role should be allowed or denied permission to use the command.</param>
    /// <param name="_">A placeholder for future use.</param>
    /// <remarks>
    ///     Offers a streamlined way to manage command permissions on a role basis,
    ///     either granting or restricting access to commands within the guild.
    /// </remarks>
    [ComponentInteraction("command_perm_spawner_rol.*.*.*$*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermsRol(string commandName, bool overwrite, bool allow, string _)
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
        // get perm overwrites targeting roles
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Role)
            .Where(x => x.State)
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
            .WithButton(GetText("perm_quick_options_overwrite"),
                $"command_perm_spawner_rol.{commandName}.{true}.{allow}$1",
                overwrite ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_overwrite:1085421377798029393>".ToIEmote(), disabled: overwrite)
            .WithButton(GetText("perm_quick_options_fallback"),
                $"command_perm_spawner_rol.{commandName}.{false}.{allow}$2",
                !overwrite ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_fallback:1085421376032231444>".ToIEmote(), disabled: !overwrite)
            .WithButton(GetText("perm_quick_options_allow"),
                $"command_perm_spawner_rol.{commandName}.{overwrite}.{true}$3",
                allow ? ButtonStyle.Success : ButtonStyle.Secondary,
                "<:perms_check:1085356998247317514>".ToIEmote(), disabled: allow)
            .WithButton(GetText("perm_quick_options_deny"),
                $"command_perm_spawner_rol.{commandName}.{overwrite}.{false}$4",
                !allow ? ButtonStyle.Danger : ButtonStyle.Secondary,
                "<:perms_disabled:1085358511900327956>".ToIEmote(), disabled: !allow);

        var i = 0;
        for (i = 0; i < Math.Min(splitGroups.Count, 3); i++)
        {
            var options = splitGroups[i]
                .Select(x => (x, role: TryGetRole(x.PrimaryTargetId)))
                .Select(x => new SelectMenuOptionBuilder(x.role?.ToString() ?? "Deleted Role", x.x.Id.ToString(),
                    GetText($"perms_quick_options_role_remove_{(allow ? "allow" : "deny")}", x.x.PrimaryTargetId),
                    "<:role:808826577785716756>".ToIEmote(), true));
            var sb = new SelectMenuBuilder($"perm_quick_options_role_remove.{commandName}.{overwrite}.{allow}${i}",
                options.ToList(), GetText("perms_quick_options_role_remove"),
                options.Count(), 0);
            cb.WithSelectMenu(sb);
        }

        cb.WithSelectMenu(
            $"perm_quick_options_role_add.{commandName}.{overwrite}.{allow}${Random.Shared.NextInt64(i, long.MaxValue)}",
            placeholder: GetText("perm_quick_options_add_roles"), minValues: 1, maxValues: 10,
            type: ComponentType.RoleSelect, options: null);

        return (Context.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }

    /// <summary>
    ///     Removes role-specific permission overrides for a command.
    /// </summary>
    /// <param name="commandName">The name of the command to modify permissions for.</param>
    /// <param name="overwrite">Indicates whether to overwrite existing permissions.</param>
    /// <param name="allow">Specifies if the permission to be removed is an allow or deny type.</param>
    /// <param name="index">The index in the permission configuration list being modified.</param>
    /// <param name="values">The roles from which to remove permission overrides.</param>
    /// <remarks>
    ///     Enables administrators to clean up permission configurations by removing outdated or unnecessary role-specific
    ///     overrides.
    /// </remarks>
    [ComponentInteraction("perm_quick_options_role_remove.*.*.*$*", true)]
    public async Task RemoveRoleOveride(string commandName, bool overwrite, bool allow, int index, string[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Role)
            .Where(x => x.State)
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
        foreach (var p in perms.Where(x => !values.Contains(x.Id.ToString())))
        {
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);
        }

        await CommandPermsRol(commandName, overwrite, allow, "");
    }

    /// <summary>
    ///     Adds role-specific permission overrides for a command.
    /// </summary>
    /// <param name="commandName">The name of the command to add permission overrides for.</param>
    /// <param name="overwrite">Indicates whether these permissions should overwrite existing ones.</param>
    /// <param name="allow">Determines whether the command is allowed or denied for the specified roles.</param>
    /// <param name="_">A placeholder parameter for future expansion.</param>
    /// <param name="values">The roles to which the permission overrides will be applied.</param>
    /// <remarks>
    ///     Facilitates granular control over command access, allowing for the specification of command permissions on a
    ///     per-role basis.
    /// </remarks>
    [ComponentInteraction("perm_quick_options_role_add.*.*.*$*", true)]
    public async Task AddRoleOveride(string commandName, bool overwrite, bool allow, string _, IRole[] values)
    {
        IList<Permissionv2> perms = Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache)
            ? permCache.Permissions.Source.ToList()
            : Permissionv2.GetDefaultPermlist;

        var matchingPerms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Role)
            .Where(x => x.State)
            .ToList();

        var needMod = values.Where(x => !matchingPerms.Any(y => y.PrimaryTargetId == x.Id));
        var needRems = perms.Where(x => needMod.Any(y => x.PrimaryTargetId == y.Id));
        var needAdd = needMod.Where(x => !needRems.Any(y => x.Id == y.PrimaryTargetId));

        var i = -1;
        foreach (var p in needRems)
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);

        var trueAdd = needAdd.Select(x => new Permissionv2
        {
            IsCustomCommand = true,
            PrimaryTarget = PrimaryPermissionType.Role,
            PrimaryTargetId = x.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = commandName,
            State = true
        });
        await Service.AddPermissions(ctx.Guild.Id, trueAdd.ToArray());

        if (!overwrite)
        {
            await CommandPermsRol(commandName, overwrite, allow, "");
            return;
        }

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        for (i = 0; i < needAdd.Count(); i++)
            await Service.UnsafeMovePerm(ctx.Guild.Id, perms.Last().Index, 1);
        await CommandPermsRol(commandName, overwrite, allow, "");
    }

    /// <summary>
    ///     Configures permissions for a command targeted at channels.
    /// </summary>
    /// <param name="commandName">The name of the command for which to configure permissions.</param>
    /// <param name="overwrite">Specifies whether to overwrite existing permission settings.</param>
    /// <param name="allow">Indicates whether the permission should allow or deny command execution in the channel.</param>
    /// <param name="_">A placeholder parameter for future use.</param>
    /// <remarks>
    ///     This method allows for detailed command permission configurations on a per-channel basis,
    ///     enhancing command access control within the guild.
    /// </remarks>
    [ComponentInteraction("command_perm_spawner_chn.*.*.*$*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermsChn(string commandName, bool overwrite, bool allow, string _)
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
        // get perm overwrites targeting users

        IList<Permissionv2> perms = Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache)
            ? permCache.Permissions.Source.ToList()
            : Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Channel)
            .Where(x => x.State)
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
            .WithButton(GetText("perm_quick_options_overwrite"),
                $"command_perm_spawner_chn.{commandName}.{true}.{allow}$1",
                overwrite ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_overwrite:1085421377798029393>".ToIEmote(), disabled: overwrite)
            .WithButton(GetText("perm_quick_options_fallback"),
                $"command_perm_spawner_chn.{commandName}.{false}.{allow}$2",
                !overwrite ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_fallback:1085421376032231444>".ToIEmote(), disabled: !overwrite)
            .WithButton(GetText("perm_quick_options_allow"),
                $"command_perm_spawner_chn.{commandName}.{overwrite}.{true}$3",
                allow ? ButtonStyle.Success : ButtonStyle.Secondary,
                "<:perms_check:1085356998247317514>".ToIEmote(), disabled: allow)
            .WithButton(GetText("perm_quick_options_deny"),
                $"command_perm_spawner_chn.{commandName}.{overwrite}.{false}$4",
                !allow ? ButtonStyle.Danger : ButtonStyle.Secondary,
                "<:perms_disabled:1085358511900327956>".ToIEmote(), disabled: !allow);

        var i = 0;
        for (i = 0; i < Math.Min(splitGroups.Count, 3); i++)
        {
            var options = splitGroups[i]
                .Select(async x => (x, channel: await TryGetChannel(x.PrimaryTargetId)))
                .Select(x => x.Result)
                .Select(x => new SelectMenuOptionBuilder(x.channel?.ToString() ?? "Deleted Channel", x.x.Id.ToString(),
                    GetText($"perms_quick_options_channel_remove_{(allow ? "allow" : "deny")}", x.x.PrimaryTargetId),
                    GetChannelEmote(x.channel), true));
            var sb = new SelectMenuBuilder($"perm_quick_options_channel_remove.{commandName}.{overwrite}.{allow}${i}",
                options.ToList(),
                GetText("perms_quick_options_channel_remove"), options.Count(), 0);
            cb.WithSelectMenu(sb);
        }

        cb.WithSelectMenu(
            $"perm_quick_options_channel_add.{commandName}.{overwrite}.{allow}${Random.Shared.NextInt64(i, long.MaxValue)}",
            placeholder: GetText("perm_quick_options_add_channels"), minValues: 1, maxValues: 10,
            type: ComponentType.ChannelSelect, options: null,
            channelTypes: Enum.GetValues<ChannelType>().Where(x => x != ChannelType.Category).ToArray());

        return (Context.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }


    /// <summary>
    ///     Removes channel-specific permission overrides for a given command.
    /// </summary>
    /// <param name="commandName">The name of the command for which to remove permissions.</param>
    /// <param name="overwrite">
    ///     Indicates if the action should overwrite existing permission settings. This parameter is not
    ///     directly used but reflects a potential future extension for handling permission updates.
    /// </param>
    /// <param name="allow">
    ///     Specifies whether the permissions being removed were initially set to allow or deny the command.
    ///     Similar to overwrite, this parameter is for future use and contextual consistency.
    /// </param>
    /// <param name="index">
    ///     The index representing the chunk of permissions being targeted for removal within a paginated
    ///     setup.
    /// </param>
    /// <param name="values">An array of channel IDs from which the command's permission overrides will be removed.</param>
    /// <remarks>
    ///     This method facilitates the dynamic management of command permissions on a per-channel basis, allowing
    ///     administrators to refine access control with precision. The implementation respects pagination, enabling scalable
    ///     permission management across numerous channels.
    /// </remarks>
    [ComponentInteraction("perm_quick_options_channel_remove.*.*.*$*", true)]
    public async Task RemoveChannelOveride(string commandName, bool overwrite, bool allow, int index, string[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Channel)
            .Where(x => x.State)
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
        foreach (var p in perms.Where(x => !values.Contains(x.Id.ToString())))
        {
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);
        }

        await CommandPermsChn(commandName, overwrite, allow, "");
    }

    /// <summary>
    ///     Adds channel-specific permission overrides for a command.
    /// </summary>
    /// <param name="commandName">The name of the command to configure permissions for.</param>
    /// <param name="overwrite">Indicates whether to overwrite existing permissions.</param>
    /// <param name="allow">Specifies whether the command is allowed or denied within the channel.</param>
    /// <param name="_">A placeholder parameter for future expansion.</param>
    /// <param name="values">The channels to which the permission overrides will be applied.</param>
    /// <remarks>
    ///     This method supports precise permission management by allowing specific command permissions
    ///     to be set or overridden in individual channels.
    /// </remarks>
    [ComponentInteraction("perm_quick_options_channel_add.*.*.*$*", true)]
    public async Task AddChannelOveride(string commandName, bool overwrite, bool allow, string _, IChannel[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        var matchingPerms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Channel)
            .Where(x => x.State)
            .ToList();

        var needMod = values.Where(x => !matchingPerms.Any(y => y.PrimaryTargetId == x.Id));
        var needRems = perms.Where(x => needMod.Any(y => x.PrimaryTargetId == y.Id));
        var needAdd = needMod.Where(x => !needRems.Any(y => x.Id == y.PrimaryTargetId));

        var i = -1;
        foreach (var p in needRems)
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);

        var trueAdd = needAdd.Select(x => new Permissionv2
        {
            IsCustomCommand = true,
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = x.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = commandName,
            State = true
        });
        await Service.AddPermissions(ctx.Guild.Id, trueAdd.ToArray());

        if (!overwrite)
        {
            await CommandPermsChn(commandName, overwrite, allow, "");
            return;
        }

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        for (i = 0; i < needAdd.Count(); i++)
            await Service.UnsafeMovePerm(ctx.Guild.Id, perms.Last().Index, 1);
        await CommandPermsChn(commandName, overwrite, allow, "");
    }

    /// <summary>
    ///     Configures permissions for a command targeted at categories.
    /// </summary>
    /// <param name="commandName">The name of the command for which to set permissions.</param>
    /// <param name="overwrite">Indicates if existing permission settings should be overwritten.</param>
    /// <param name="allow">Determines if the permission should enable or disable command use in the category.</param>
    /// <param name="_">A placeholder parameter for potential future use.</param>
    /// <remarks>
    ///     Enables fine-tuned control over command permissions within category channels, enhancing customization of command
    ///     access.
    /// </remarks>
    [ComponentInteraction("command_perm_spawner_cat.*.*.*$*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandPermsCat(string commandName, bool overwrite, bool allow, string _)
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
        // get perm overwrites targeting users
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Category)
            .Where(x => x.State)
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
            .WithButton(GetText("perm_quick_options_overwrite"),
                $"command_perm_spawner_cat.{commandName}.{true}.{allow}$1",
                overwrite ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_overwrite:1085421377798029393>".ToIEmote(), disabled: overwrite)
            .WithButton(GetText("perm_quick_options_fallback"),
                $"command_perm_spawner_cat.{commandName}.{false}.{allow}$2",
                !overwrite ? ButtonStyle.Primary : ButtonStyle.Secondary,
                "<:perms_fallback:1085421376032231444>".ToIEmote(), disabled: !overwrite)
            .WithButton(GetText("perm_quick_options_allow"),
                $"command_perm_spawner_cat.{commandName}.{overwrite}.{true}$3",
                allow ? ButtonStyle.Success : ButtonStyle.Secondary,
                "<:perms_check:1085356998247317514>".ToIEmote(), disabled: allow)
            .WithButton(GetText("perm_quick_options_deny"),
                $"command_perm_spawner_cat.{commandName}.{overwrite}.{false}$4",
                !allow ? ButtonStyle.Danger : ButtonStyle.Secondary,
                "<:perms_disabled:1085358511900327956>".ToIEmote(), disabled: !allow);

        var i = 0;
        for (i = 0; i < Math.Min(splitGroups.Count, 3); i++)
        {
            var options = splitGroups[i]
                .Select(async x => (x, channel: await TryGetChannel(x.PrimaryTargetId)))
                .Select(x => x.Result)
                .Select(x => new SelectMenuOptionBuilder(x.channel?.ToString() ?? "Deleted Channel", x.x.Id.ToString(),
                    GetText($"perms_quick_options_category_remove_{(allow ? "allow" : "deny")}", x.x.PrimaryTargetId),
                    GetChannelEmote(x.channel), true));
            var sb = new SelectMenuBuilder($"perm_quick_options_category_remove.{commandName}.{overwrite}.{allow}${i}",
                options.ToList(),
                GetText("perms_quick_options_category_remove"), options.Count(), 0);
            cb.WithSelectMenu(sb);
        }

        cb.WithSelectMenu(
            $"perm_quick_options_category_add.{commandName}.{overwrite}.{allow}${Random.Shared.NextInt64(i, long.MaxValue)}",
            placeholder: GetText("perm_quick_options_add_categories"), minValues: 1, maxValues: 10,
            type: ComponentType.ChannelSelect, options: null, channelTypes:
            [
                ChannelType.Category
            ]);

        return (Context.Interaction as SocketMessageComponent).UpdateAsync(x => x.Components = cb.Build());
    }

    /// <summary>
    ///     Removes category-specific permission overrides for a command.
    /// </summary>
    /// <param name="commandName">The name of the command for which to remove permissions.</param>
    /// <param name="overwrite">Specifies whether the permission change should overwrite existing permissions.</param>
    /// <param name="allow">Indicates whether the permission being removed was an allow or deny permission.</param>
    /// <param name="index">The index within the configuration list from which to remove permissions.</param>
    /// <param name="values">The categories from which to remove permission overrides.</param>
    /// <remarks>
    ///     Assists in cleaning up and streamlining permission configurations by removing specific category overrides
    ///     that are no longer required.
    /// </remarks>
    [ComponentInteraction("perm_quick_options_category_remove.*.*.*$*", true)]
    public async Task RemoveCategoryOveride(string commandName, bool overwrite, bool allow, int index, string[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        perms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Category)
            .Where(x => x.State)
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
        foreach (var p in perms.Where(x => !values.Contains(x.Id.ToString())))
        {
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);
        }

        await CommandPermsChn(commandName, overwrite, allow, "");
    }

    /// <summary>
    ///     Adds category-specific permission overrides for a command.
    /// </summary>
    /// <param name="commandName">The name of the command to add permission overrides for.</param>
    /// <param name="overwrite">Specifies if the permission addition should overwrite existing permissions.</param>
    /// <param name="allow">Determines whether the added permission is an allow or deny permission for the category.</param>
    /// <param name="_">A placeholder parameter for future expansion.</param>
    /// <param name="values">The categories to which the permission overrides will be applied.</param>
    /// <remarks>
    ///     This method allows for detailed permission management, enabling or restricting command usage
    ///     in specific categories as needed.
    /// </remarks>
    [ComponentInteraction("perm_quick_options_category_add.*.*.*$*", true)]
    public async Task AddCategoryOveride(string commandName, bool overwrite, bool allow, string _, IChannel[] values)
    {
        IList<Permissionv2> perms;

        if (Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache))
            perms = permCache.Permissions.Source.ToList();
        else
            perms = Permissionv2.GetDefaultPermlist;

        var matchingPerms = perms
            .Where(x => x.SecondaryTargetName == commandName)
            .Where(x => x.PrimaryTarget == PrimaryPermissionType.Category)
            .Where(x => x.State)
            .ToList();

        var needMod = values.Where(x => !matchingPerms.Any(y => y.PrimaryTargetId == x.Id));
        var needRems = perms.Where(x => needMod.Any(y => x.PrimaryTargetId == y.Id));
        var needAdd = needMod.Where(x => !needRems.Any(y => x.Id == y.PrimaryTargetId));

        var i = -1;
        foreach (var p in needRems)
            await Service.RemovePerm(ctx.Guild.Id, p.Index - ++i);

        var trueAdd = needAdd.Select(x => new Permissionv2
        {
            IsCustomCommand = true,
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = x.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = commandName,
            State = true
        });
        await Service.AddPermissions(ctx.Guild.Id, trueAdd.ToArray());

        if (!overwrite)
        {
            await CommandPermsCat(commandName, overwrite, allow, "");
            return;
        }

        perms = Service.Cache.TryGetValue(ctx.Guild.Id, out permCache)
            ? permCache.Permissions.Source.ToList()
            : Permissionv2.GetDefaultPermlist;

        for (i = 0; i < needAdd.Count(); i++)
            await Service.UnsafeMovePerm(ctx.Guild.Id, perms.Last().Index, 1);
        await CommandPermsCat(commandName, overwrite, allow, "");
    }

    /// <summary>
    ///     Attempts to retrieve a user by their unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <returns>A task that represents the asynchronous operation, with the user if found; otherwise, null.</returns>
    /// <remarks>
    ///     This method encapsulates error handling for user retrieval, returning null if the user cannot be found or an error
    ///     occurs.
    /// </remarks>
    private async Task<IUser?> TryGetUser(ulong id)
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

    /// <summary>
    ///     Attempts to retrieve a role by its unique identifier within the guild context.
    /// </summary>
    /// <param name="id">The unique identifier of the role.</param>
    /// <returns>The role if found; otherwise, null.</returns>
    /// <remarks>
    ///     Utilizes the guild's role collection to find a role, ensuring the role is relevant to the current guild context.
    /// </remarks>
    private IRole? TryGetRole(ulong id)
    {
        return Context.Guild.Roles.FirstOrDefault(x => x.Id == id);
    }

    /// <summary>
    ///     Attempts to retrieve a channel by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the channel.</param>
    /// <returns>A task that represents the asynchronous operation, with the channel if found; otherwise, null.</returns>
    /// <remarks>
    ///     This method encapsulates error handling for channel retrieval, returning null if the channel cannot be found or an
    ///     error occurs.
    /// </remarks>
    private async Task<IChannel?> TryGetChannel(ulong id)
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

    /// <summary>
    ///     Retrieves an emote corresponding to the type of a given channel.
    /// </summary>
    /// <param name="channel">The channel for which to retrieve the emote.</param>
    /// <returns>An emote that visually represents the type of the given channel.</returns>
    /// <remarks>
    ///     This method allows for visual differentiation of channel types in user interfaces through the use of specific
    ///     emotes.
    /// </remarks>
    private IEmote GetChannelEmote(IChannel channel)
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