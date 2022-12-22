using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions : MewdekoModuleBase<PermissionService>
{
    public enum Reset
    {
        Reset
    }

    private readonly DbService db;
    private readonly InteractiveService interactivity;
    private readonly GuildSettingsService guildSettings;

    public Permissions(DbService db, InteractiveService inter, GuildSettingsService guildSettings)
    {
        interactivity = inter;
        this.guildSettings = guildSettings;
        this.db = db;
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task ResetPerms()
    {
        await Service.Reset(ctx.Guild.Id).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("perms_reset").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Verbose(PermissionAction? action = null)
    {
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var config = await uow.GcWithPermissionsv2For(ctx.Guild.Id);
            action ??= new PermissionAction(!config.VerbosePermissions);
            config.VerbosePermissions = action.Value;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);
        }

        if (action.Value)
            await ReplyConfirmLocalizedAsync("verbose_true").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("verbose_false").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator), Priority(0)]
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
                await ReplyConfirmLocalizedAsync("permrole_not_set", Format.Bold(cache.PermRole))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("permrole", Format.Bold(role.ToString())).ConfigureAwait(false);
            }

            return;
        }

        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var config = await uow.GcWithPermissionsv2For(ctx.Guild.Id);
            config.PermissionRole = role.Id.ToString();
            await uow.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);
        }

        await ReplyConfirmLocalizedAsync("permrole_changed", Format.Bold(role.Name)).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator), Priority(1)]
    public async Task PermRole(Reset _)
    {
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var config = await uow.GcWithPermissionsv2For(ctx.Guild.Id);
            config.PermissionRole = null;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            Service.UpdateCache(config);
        }

        await ReplyConfirmLocalizedAsync("permrole_reset").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task ListPerms()
    {
        IList<Permissionv2> perms = Service.Cache.TryGetValue(ctx.Guild.Id, out var permCache) ? permCache.Permissions.Source.ToList() : Permissionv2.GetDefaultPermlist;
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(perms.Count / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();
        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task RemovePerm(int index)
    {
        index--;
        if (index < 0)
            return;
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

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task MovePerm(int from, int to)
    {
        from--;
        to--;
        if (!(from == to || from < 0 || to < 0))
        {
            try
            {
                Permissionv2 fromPerm;
                var uow = db.GetDbContext();
                await using (uow.ConfigureAwait(false))
                {
                    var config = await uow.GcWithPermissionsv2For(ctx.Guild.Id);
                    var permsCol = new PermissionsCollection<Permissionv2>(config.Permissions);

                    var fromFound = from < permsCol.Count;
                    var toFound = to < permsCol.Count;

                    if (!fromFound)
                    {
                        await ReplyErrorLocalizedAsync("not_found", ++from).ConfigureAwait(false);
                        return;
                    }

                    if (!toFound)
                    {
                        await ReplyErrorLocalizedAsync("not_found", ++to).ConfigureAwait(false);
                        return;
                    }

                    fromPerm = permsCol[from];

                    permsCol.RemoveAt(from);
                    permsCol.Insert(to, fromPerm);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    Service.UpdateCache(config);
                }

                await ReplyConfirmLocalizedAsync("moved_permission",
                        Format.Code(fromPerm.GetCommand(await guildSettings.GetPrefix(ctx.Guild), (SocketGuild)ctx.Guild)),
                        ++from,
                        ++to)
                    .ConfigureAwait(false);
                return;
            }
            catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException)
            {
            }
        }

        await ReplyErrorLocalizedAsync("perm_out_of_range").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task SrvrCmd(CommandOrCrInfo command, PermissionAction action)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.Name.ToLowerInvariant(),
            State = action.Value,
            IsCustomCommand = command.IsCustom
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmLocalizedAsync("sx_enable",
                Format.Code(command.Name),
                GetText("of_command")).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("sx_disable",
                Format.Code(command.Name),
                GetText("of_command")).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task SrvrMdl(ModuleOrCrInfo module, PermissionAction action)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.Name.ToLowerInvariant(),
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmLocalizedAsync("sx_enable",
                Format.Code(module.Name),
                GetText("of_module")).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("sx_disable",
                Format.Code(module.Name),
                GetText("of_module")).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task UsrCmd(CommandOrCrInfo command, PermissionAction action, [Remainder] IGuildUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.Name.ToLowerInvariant(),
            State = action.Value,
            IsCustomCommand = command.IsCustom
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmLocalizedAsync("ux_enable",
                Format.Code(command.Name),
                GetText("of_command"),
                Format.Code(user.ToString())).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("ux_disable",
                Format.Code(command.Name),
                GetText("of_command"),
                Format.Code(user.ToString())).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task UsrMdl(ModuleOrCrInfo module, PermissionAction action, [Remainder] IGuildUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.Name.ToLowerInvariant(),
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmLocalizedAsync("ux_enable",
                Format.Code(module.Name),
                GetText("of_module"),
                Format.Code(user.ToString())).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("ux_disable",
                Format.Code(module.Name),
                GetText("of_module"),
                Format.Code(user.ToString())).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task RoleCmd(CommandOrCrInfo command, PermissionAction action, [Remainder] IRole role)
    {
        if (role == role.Guild.EveryoneRole)
            return;

        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Role,
            PrimaryTargetId = role.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.Name.ToLowerInvariant(),
            State = action.Value,
            IsCustomCommand = command.IsCustom
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmLocalizedAsync("rx_enable",
                Format.Code(command.Name),
                GetText("of_command"),
                Format.Code(role.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("rx_disable",
                Format.Code(command.Name),
                GetText("of_command"),
                Format.Code(role.Name)).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task RoleMdl(ModuleOrCrInfo module, PermissionAction action, [Remainder] IRole role)
    {
        if (role == role.Guild.EveryoneRole)
            return;

        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Role,
            PrimaryTargetId = role.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.Name.ToLowerInvariant(),
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmLocalizedAsync("rx_enable",
                Format.Code(module.Name),
                GetText("of_module"),
                Format.Code(role.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("rx_disable",
                Format.Code(module.Name),
                GetText("of_module"),
                Format.Code(role.Name)).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task ChnlCmd(CommandOrCrInfo command, PermissionAction action, [Remainder] ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.Name.ToLowerInvariant(),
            State = action.Value,
            IsCustomCommand = command.IsCustom
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmLocalizedAsync("cx_enable",
                Format.Code(command.Name),
                GetText("of_command"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("cx_disable",
                Format.Code(command.Name),
                GetText("of_command"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task ChnlMdl(ModuleOrCrInfo module, PermissionAction action, [Remainder] ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.Name.ToLowerInvariant(),
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmLocalizedAsync("cx_enable",
                Format.Code(module.Name),
                GetText("of_module"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("cx_disable",
                Format.Code(module.Name),
                GetText("of_module"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task AllChnlMdls(PermissionAction action, [Remainder] ITextChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Channel,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
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

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task CatCmd(CommandOrCrInfo command, PermissionAction action, [Remainder] ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Command,
            SecondaryTargetName = command.Name.ToLowerInvariant(),
            State = action.Value,
            IsCustomCommand = command.IsCustom
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmLocalizedAsync("cx_enable",
                Format.Code(command.Name),
                GetText("of_command"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("cx_disable",
                Format.Code(command.Name),
                GetText("of_command"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task CatMdl(ModuleOrCrInfo module, PermissionAction action, [Remainder] ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.Module,
            SecondaryTargetName = module.Name.ToLowerInvariant(),
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
        {
            await ReplyConfirmLocalizedAsync("cx_enable",
                Format.Code(module.Name),
                GetText("of_module"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("cx_disable",
                Format.Code(module.Name),
                GetText("of_module"),
                Format.Code(chnl.Name)).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task AllCatMdls(PermissionAction action, [Remainder] ICategoryChannel chnl)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Category,
            PrimaryTargetId = chnl.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
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

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task AllRoleMdls(PermissionAction action, [Remainder] IRole role)
    {
        if (role == role.Guild.EveryoneRole)
            return;

        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Role,
            PrimaryTargetId = role.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
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

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task AllUsrMdls(PermissionAction action, [Remainder] IUser user)
    {
        await Service.AddPermissions(ctx.Guild.Id, new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.User,
            PrimaryTargetId = user.Id,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.Value
        }).ConfigureAwait(false);

        if (action.Value)
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

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task AllSrvrMdls(PermissionAction action)
    {
        var newPerm = new Permissionv2
        {
            PrimaryTarget = PrimaryPermissionType.Server,
            PrimaryTargetId = 0,
            SecondaryTarget = SecondaryPermissionType.AllModules,
            SecondaryTargetName = "*",
            State = action.Value
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

        if (action.Value)
            await ReplyConfirmLocalizedAsync("asm_enable").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("asm_disable").ConfigureAwait(false);
    }
}