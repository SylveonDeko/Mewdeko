using DataModel;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

/// <summary>
///     A module for managing permissions for commands.
/// </summary>
/// <param name="dbFactory">The database service.</param>
/// <param name="inter">The interactive service.</param>
/// <param name="guildSettings">The guild settings service.</param>
public partial class Permissions(
    IDataConnectionFactory dbFactory,
    InteractiveService inter,
    GuildSettingsService guildSettings)
    : MewdekoModuleBase<PermissionService>
{
    /// <summary>
    ///     Used with the permrole command to reset the permission role.
    /// </summary>
    public enum Reset
    {
        /// <summary>
        ///     Resets the permission role.
        /// </summary>
        Reset
    }

    /// <summary>
    ///     Resets the permissions for the guild.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ResetPerms()
    {
        await Service.Reset(ctx.Guild.Id).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.PermsReset(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets whether commands should throw an error based on what the issue is when using a command.
    /// </summary>
    /// <param name="action">Just a true or false thing. Kinda useless since its a toggle anyway.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Verbose(PermissionAction? action = null)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Get GuildConfig directly
        var config = await guildSettings.GetGuildConfig(ctx.Guild.Id);

        action ??= new PermissionAction(!config.VerbosePermissions);
        config.VerbosePermissions = action.Value;

        await guildSettings.UpdateGuildConfig(ctx.Guild.Id, config);

        // Get permissions for cache update
        var permissions = await dbContext.Permissions1
            .Where(p => p.GuildId == ctx.Guild.Id)
            .ToListAsync();

        Service.UpdateCache(ctx.Guild.Id, permissions, config);

        if (action.Value)
            await ReplyConfirmAsync(Strings.VerboseTrue(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.VerboseFalse(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the role that will be used for permissions. If no role is provided, it will show the current permission role.
    /// </summary>
    /// <param name="role">The role, if any, to set as the permissions role</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [Priority(0)]
    public async Task PermRole([Remainder] IRole? role = null)
    {
        if (role != null && role == role.Guild.EveryoneRole)
            return;

        if (role == null)
        {
            var cache = await Service.GetCacheFor(ctx.Guild.Id);
            if (!ulong.TryParse(cache.PermRole, out var roleId) ||
                (role = ((SocketGuild)ctx.Guild).GetRole(roleId)) == null)
            {
                await ReplyConfirmAsync(Strings.PermroleNotSet(ctx.Guild.Id))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.Permrole(ctx.Guild.Id, Format.Bold(role.ToString())))
                    .ConfigureAwait(false);
            }

            return;
        }

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Get GuildConfig directly
        var config = await guildSettings.GetGuildConfig(ctx.Guild.Id);

        config.PermissionRole = role.Id.ToString();


        // Get permissions for cache update
        var permissions = await dbContext.Permissions1
            .Where(p => p.GuildId == ctx.Guild.Id)
            .ToListAsync();

        Service.UpdateCache(ctx.Guild.Id, permissions, config);

        await ReplyConfirmAsync(Strings.PermroleChanged(ctx.Guild.Id, Format.Bold(role.Name))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Resets the permission role.
    /// </summary>
    /// <param name="_"></param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [Priority(1)]
    public async Task PermRole(Reset _)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Get GuildConfig directly
        var config = await guildSettings.GetGuildConfig(ctx.Guild.Id);

        config.PermissionRole = null;

        await guildSettings.UpdateGuildConfig(ctx.Guild.Id, config);


        // Get permissions for cache update
        var permissions = await dbContext.Permissions1
            .Where(p => p.GuildId == ctx.Guild.Id)
            .ToListAsync();

        Service.UpdateCache(ctx.Guild.Id, permissions, config);

        await ReplyConfirmAsync(Strings.PermroleReset(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the permissions for the guild.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ListPerms()
    {
        IList<Permission1> perms = Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache)
            ? permCache.Permissions.ToList()
            : PermissionExtensions.GetDefaultPermlist;
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(perms.Count / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();
        await inter.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            var prefix = await guildSettings.GetPrefix(ctx.Guild).ConfigureAwait(false);
            return new PageBuilder().WithDescription(string.Join("\n",
                perms.Skip(page * 10).Take(10).Select(p =>
                {
                    var str =
                        $"`{p.Index + 1}.` {Format.Bold(p.GetCommand(prefix, (SocketGuild)ctx.Guild))}";
                    if (p.Index == 0)
                        str += $" [{Strings.Uneditable(ctx.Guild.Id)}]";
                    return str;
                }))).WithTitle(Format.Bold(Strings.Page(ctx.Guild.Id, page + 1))).WithOkColor();
        }
    }

    /// <summary>
    ///     Removes a permission from the list based on its index.
    /// </summary>
    /// <param name="index">The perm to remove</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task RemovePerm(int index)
    {
        index--;
        if (index < 0)
            return;
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();

            // Get permissions directly
            var permissions = await dbContext.Permissions1
                .Where(p => p.GuildId == ctx.Guild.Id)
                .OrderBy(p => p.Index)
                .ToListAsync();

            // Get GuildConfig for cache update
            var config = await guildSettings.GetGuildConfig(ctx.Guild.Id);

            var permsCol = new List<Permission1>(permissions);
            var p = permsCol[index];
            permsCol.RemoveAt(index);
            await dbContext.DeleteAsync(p);


            Service.UpdateCache(ctx.Guild.Id, permsCol.ToList(), config);

            await ReplyConfirmAsync(Strings.Removed(ctx.Guild.Id,
                    index + 1,
                    Format.Code(p.GetCommand(await guildSettings.GetPrefix(ctx.Guild), ctx.Guild as SocketGuild))))
                .ConfigureAwait(false);
        }
        catch (IndexOutOfRangeException)
        {
            await ReplyErrorAsync(Strings.PermOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Moves a permission higher in the heirarchy.
    /// </summary>
    /// <param name="from">Initial Index</param>
    /// <param name="to">Replacement index</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task MovePerm(int from, int to)
    {
        from--;
        to--;
        if (!(from == to || from < 0 || to < 0))
        {
            try
            {
                await using var dbContext = await dbFactory.CreateConnectionAsync();

                // Get permissions directly
                var permissions = await dbContext.Permissions1
                    .Where(p => p.GuildId == ctx.Guild.Id)
                    .OrderBy(p => p.Index)
                    .ToListAsync();

                // Get GuildConfig for cache update
                var config = await guildSettings.GetGuildConfig(ctx.Guild.Id);

                var permsCol = new List<Permission1>(permissions);

                var fromFound = from < permsCol.Count;
                var toFound = to < permsCol.Count;

                if (!fromFound)
                {
                    await ReplyErrorAsync(Strings.PermNotFound(ctx.Guild.Id, ++from)).ConfigureAwait(false);
                    return;
                }

                if (!toFound)
                {
                    await ReplyErrorAsync(Strings.PermNotFound(ctx.Guild.Id, ++to)).ConfigureAwait(false);
                    return;
                }

                var fromPerm = permsCol[from];

                permsCol.RemoveAt(from);
                permsCol.Insert(to, fromPerm);

                // Update indices
                for (var i = 0; i < permsCol.Count; i++)
                {
                    permsCol[i].Index = i;
                    await dbContext.UpdateAsync(permsCol[i]);
                }


                Service.UpdateCache(ctx.Guild.Id, permsCol.ToList(), config);

                await ReplyConfirmAsync(Strings.MovedPermission(ctx.Guild.Id,
                        Format.Code(fromPerm.GetCommand(await guildSettings.GetPrefix(ctx.Guild),
                            (SocketGuild)ctx.Guild)),
                        ++from,
                        ++to))
                    .ConfigureAwait(false);
                return;
            }
            catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException)
            {
            }
        }

        await ReplyErrorAsync(Strings.PermOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Enables or disables a command in the server.
    /// </summary>
    /// <param name="command">The command to run an action on</param>
    /// <param name="action">Whether to disable or enable the command</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task SrvrCmd(CommandOrCrInfo command, PermissionAction action)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = (int)SecondaryPermissionType.Command,
            SecondaryTargetName = command.Name.ToLowerInvariant(),
            State = action.Value,
            IsCustomCommand = command.IsCustom
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.SxEnable(ctx.Guild.Id,
                Format.Code(command.Name),
                Strings.OfCommand(ctx.Guild.Id))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.SxDisable(ctx.Guild.Id,
                Format.Code(command.Name),
                Strings.OfCommand(ctx.Guild.Id))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes server-level permissions for a specific module.
    /// </summary>
    /// <param name="module">The module to set permissions for.</param>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <remarks>
    ///     This method allows setting permissions for a particular module at the server level.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task SrvrMdl(ModuleOrCrInfo module, PermissionAction action)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = (int)SecondaryPermissionType.Module,
            SecondaryTargetName = module.Name.ToLowerInvariant(),
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.SxEnable(ctx.Guild.Id,
                Format.Code(module.Name),
                Strings.OfModule(ctx.Guild.Id))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.SxDisable(ctx.Guild.Id,
                Format.Code(module.Name),
                Strings.OfModule(ctx.Guild.Id))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes user-specific permissions for a specific command.
    /// </summary>
    /// <param name="command">The command to set permissions for.</param>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="user">The user to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for a particular command for a specific user.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task UsrCmd(CommandOrCrInfo command, PermissionAction action, [Remainder] IGuildUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = (int)SecondaryPermissionType.Command,
            SecondaryTargetName = command.Name.ToLowerInvariant(),
            State = action.Value,
            IsCustomCommand = command.IsCustom
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.UxEnable(ctx.Guild.Id,
                Format.Code(command.Name),
                Strings.OfCommand(ctx.Guild.Id),
                Format.Code(user.ToString()))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.UxDisable(ctx.Guild.Id,
                Format.Code(command.Name),
                Strings.OfCommand(ctx.Guild.Id),
                Format.Code(user.ToString()))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes user-specific permissions for a specific module.
    /// </summary>
    /// <param name="module">The module to set permissions for.</param>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="user">The user to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for a particular module for a specific user.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task UsrMdl(ModuleOrCrInfo module, PermissionAction action, [Remainder] IGuildUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = (int)SecondaryPermissionType.Module,
            SecondaryTargetName = module.Name.ToLowerInvariant(),
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.UxEnable(ctx.Guild.Id,
                Format.Code(module.Name),
                Strings.OfModule(ctx.Guild.Id),
                Format.Code(user.ToString()))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.UxDisable(ctx.Guild.Id,
                Format.Code(module.Name),
                Strings.OfModule(ctx.Guild.Id),
                Format.Code(user.ToString()))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes role-specific permissions for a specific command.
    /// </summary>
    /// <param name="command">The command to set permissions for.</param>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="role">The role to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for a particular command for a specific role.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task RoleCmd(CommandOrCrInfo command, PermissionAction action, [Remainder] IRole role)
    {
        if (role == role.Guild.EveryoneRole)
            return;

        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Role,
            PrimaryTargetId = role.Id,
            SecondaryTarget = (int)SecondaryPermissionType.Command,
            SecondaryTargetName = command.Name.ToLowerInvariant(),
            State = action.Value,
            IsCustomCommand = command.IsCustom
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.RxEnable(ctx.Guild.Id,
                Format.Code(command.Name),
                Strings.OfCommand(ctx.Guild.Id),
                Format.Code(role.Name))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.RxDisable(ctx.Guild.Id,
                Format.Code(command.Name),
                Strings.OfCommand(ctx.Guild.Id),
                Format.Code(role.Name))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes role-specific permissions for a specific module.
    /// </summary>
    /// <param name="module">The module to set permissions for.</param>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="role">The role to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for a particular module for a specific role.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task RoleMdl(ModuleOrCrInfo module, PermissionAction action, [Remainder] IRole role)
    {
        if (role == role.Guild.EveryoneRole)
            return;

        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Role,
            PrimaryTargetId = role.Id,
            SecondaryTarget = (int)SecondaryPermissionType.Module,
            SecondaryTargetName = module.Name.ToLowerInvariant(),
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.RxEnable(ctx.Guild.Id,
                Format.Code(module.Name),
                Strings.OfModule(ctx.Guild.Id),
                Format.Code(role.Name))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.RxDisable(ctx.Guild.Id,
                Format.Code(module.Name),
                Strings.OfModule(ctx.Guild.Id),
                Format.Code(role.Name))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes channel-specific permissions for a specific command.
    /// </summary>
    /// <param name="command">The command to set permissions for.</param>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="chnl">The channel to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for a particular command for a specific channel.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ChnlCmd(CommandOrCrInfo command, PermissionAction action, [Remainder] ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = (int)SecondaryPermissionType.Command,
            SecondaryTargetName = command.Name.ToLowerInvariant(),
            State = action.Value,
            IsCustomCommand = command.IsCustom
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.CxEnable(ctx.Guild.Id,
                Format.Code(command.Name),
                Strings.OfCommand(ctx.Guild.Id),
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.CxDisable(ctx.Guild.Id,
                Format.Code(command.Name),
                Strings.OfCommand(ctx.Guild.Id),
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes channel-specific permissions for a specific module.
    /// </summary>
    /// <param name="module">The module to set permissions for.</param>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="chnl">The channel to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for a particular module for a specific channel.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ChnlMdl(ModuleOrCrInfo module, PermissionAction action, [Remainder] ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = (int)SecondaryPermissionType.Module,
            SecondaryTargetName = module.Name.ToLowerInvariant(),
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.CxEnable(ctx.Guild.Id,
                Format.Code(module.Name),
                Strings.OfModule(ctx.Guild.Id),
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.CxDisable(ctx.Guild.Id,
                Format.Code(module.Name),
                Strings.OfModule(ctx.Guild.Id),
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes permissions for all modules in a specific channel.
    /// </summary>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="chnl">The channel to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for all modules in a specific channel.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AllChnlMdls(PermissionAction action, [Remainder] ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = (int)SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.AcmEnable(ctx.Guild.Id,
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.AcmDisable(ctx.Guild.Id,
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes command-specific permissions for a specific category.
    /// </summary>
    /// <param name="command">The command to set permissions for.</param>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="chnl">The category to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for a particular command for a specific category.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task CatCmd(CommandOrCrInfo command, PermissionAction action, [Remainder] ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = (int)SecondaryPermissionType.Command,
            SecondaryTargetName = command.Name.ToLowerInvariant(),
            State = action.Value,
            IsCustomCommand = command.IsCustom
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.CxEnable(ctx.Guild.Id,
                Format.Code(command.Name),
                Strings.OfCommand(ctx.Guild.Id),
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.CxDisable(ctx.Guild.Id,
                Format.Code(command.Name),
                Strings.OfCommand(ctx.Guild.Id),
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes module-specific permissions for a specific category.
    /// </summary>
    /// <param name="module">The module to set permissions for.</param>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="chnl">The category to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for a particular module for a specific category.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task CatMdl(ModuleOrCrInfo module, PermissionAction action, [Remainder] ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = (int)SecondaryPermissionType.Module,
            SecondaryTargetName = module.Name.ToLowerInvariant(),
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.CxEnable(ctx.Guild.Id,
                Format.Code(module.Name),
                Strings.OfModule(ctx.Guild.Id),
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.CxDisable(ctx.Guild.Id,
                Format.Code(module.Name),
                Strings.OfModule(ctx.Guild.Id),
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes permissions for all modules in a specific category.
    /// </summary>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="chnl">The category to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for all modules in a specific category.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AllCatMdls(PermissionAction action, [Remainder] ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = (int)SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.AcmEnable(ctx.Guild.Id,
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.AcmDisable(ctx.Guild.Id,
                Format.Code(chnl.Name))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes permissions for all modules for a specific role.
    /// </summary>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="role">The role to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for all modules for a specific role.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AllRoleMdls(PermissionAction action, [Remainder] IRole role)
    {
        if (role == role.Guild.EveryoneRole)
            return;

        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Role,
            PrimaryTargetId = role.Id,
            SecondaryTarget = (int)SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.ArmEnable(ctx.Guild.Id,
                Format.Code(role.Name))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.ArmDisable(ctx.Guild.Id,
                Format.Code(role.Name))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes permissions for all modules for a specific user.
    /// </summary>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <param name="user">The user to set permissions for.</param>
    /// <remarks>
    ///     This method allows setting permissions for all modules for a specific user.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AllUsrMdls(PermissionAction action, [Remainder] IUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = (int)SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmAsync(Strings.AumEnable(ctx.Guild.Id,
                Format.Code(user.ToString()))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.AumDisable(ctx.Guild.Id,
                Format.Code(user.ToString()))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes permissions for all modules for the entire server.
    /// </summary>
    /// <param name="action">The action to perform (enable/disable).</param>
    /// <remarks>
    ///     This method allows setting permissions for all modules for all users in the server.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AllSrvrMdls(PermissionAction action)
    {
        var newPerm = new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = (int)SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.Value
        };

        var allowUser = new Permission1
        {
            PrimaryTarget = (int)PrimaryPermissionType.User,
            PrimaryTargetId = ctx.User.Id,
            SecondaryTarget = (int)SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = true
        };

        await Service.AddPermissions(ctx.Guild.Id,
            newPerm,
            allowUser).ConfigureAwait(false);

        if (action.Value)
            await ReplyConfirmAsync(Strings.AsmEnable(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.AsmDisable(ctx.Guild.Id)).ConfigureAwait(false);
    }
}