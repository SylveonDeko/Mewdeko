using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Collections;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;

namespace Mewdeko.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class FlowerShopCommands : MewdekoSubmodule
        {
            private readonly DbService _db;
            private readonly ICurrencyService _cs;
            private readonly DiscordSocketClient _client;

            public enum Role
            {
                Role
            }

            public enum List
            {
                List
            }

            public FlowerShopCommands(DbService db, ICurrencyService cs, DiscordSocketClient client)
            {
                _db = db;
                _cs = cs;
                _client = client;
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Shop(int page = 1)
            {
                if (--page < 0)
                    return;
                List<ShopEntry> entries;
                using (var uow = _db.GetDbContext())
                {
                    entries = new IndexedCollection<ShopEntry>(uow.GuildConfigs.ForId(ctx.Guild.Id,
                        set => set.Include(x => x.ShopEntries)
                                  .ThenInclude(x => x.Items)).ShopEntries);
                }

                await ctx.SendPaginatedConfirmAsync(page, (curPage) =>
                {
                    var theseEntries = entries.Skip(curPage * 9).Take(9).ToArray();

                    if (!theseEntries.Any())
                        return new EmbedBuilder().WithErrorColor()
                            .WithDescription(GetText("shop_none"));
                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle(GetText("shop", Bc.BotConfig.CurrencySign));

                    for (int i = 0; i < theseEntries.Length; i++)
                    {
                        var entry = theseEntries[i];
                        embed.AddField(efb => efb.WithName($"#{curPage * 9 + i + 1} - {entry.Price}{Bc.BotConfig.CurrencySign}").WithValue(EntryToString(entry)).WithIsInline(true));
                    }
                    return embed;
                }, entries.Count, 9, true).ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Buy(int index)
            {
                index -= 1;
                if (index < 0)
                    return;
                ShopEntry entry;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(ctx.Guild.Id, set => set
                        .Include(x => x.ShopEntries)
                        .ThenInclude(x => x.Items));
                    var entries = new IndexedCollection<ShopEntry>(config.ShopEntries);
                    entry = entries.ElementAtOrDefault(index);
                    await uow.SaveChangesAsync();
                }

                if (entry == null)
                {
                    await ReplyErrorLocalizedAsync("shop_item_not_found").ConfigureAwait(false);
                    return;
                }

                if (entry.Type == ShopEntryType.Role)
                {
                    var guser = (IGuildUser)ctx.User;
                    var role = ctx.Guild.GetRole(entry.RoleId);

                    if (role == null)
                    {
                        await ReplyErrorLocalizedAsync("shop_role_not_found").ConfigureAwait(false);
                        return;
                    }

                    if (await _cs.RemoveAsync(ctx.User.Id, $"Shop purchase - {entry.Type}", entry.Price).ConfigureAwait(false))
                    {
                        try
                        {
                            await guser.AddRoleAsync(role).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _log.Warn(ex);
                            await _cs.AddAsync(ctx.User.Id, $"Shop error refund", entry.Price).ConfigureAwait(false);
                            await ReplyErrorLocalizedAsync("shop_role_purchase_error").ConfigureAwait(false);
                            return;
                        }
                        var profit = GetProfitAmount(entry.Price);
                        await _cs.AddAsync(entry.AuthorId, $"Shop sell item - {entry.Type}", profit).ConfigureAwait(false);
                        await _cs.AddAsync(ctx.Client.CurrentUser.Id, $"Shop sell item - cut", entry.Price - profit).ConfigureAwait(false);
                        await ReplyConfirmLocalizedAsync("shop_role_purchase", Format.Bold(role.Name)).ConfigureAwait(false);
                        return;
                    }
                    else
                    {
                        await ReplyErrorLocalizedAsync("not_enough", Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                        return;
                    }
                }
                else if (entry.Type == ShopEntryType.List)
                {
                    if (entry.Items.Count == 0)
                    {
                        await ReplyErrorLocalizedAsync("out_of_stock").ConfigureAwait(false);
                        return;
                    }

                    var item = entry.Items.ToArray()[new MewdekoRandom().Next(0, entry.Items.Count)];

                    if (await _cs.RemoveAsync(ctx.User.Id, $"Shop purchase - {entry.Type}", entry.Price).ConfigureAwait(false))
                    {
                        using (var uow = _db.GetDbContext())
                        {
                            var x = uow._context.Set<ShopEntryItem>().Remove(item);
                            await uow.SaveChangesAsync();
                        }
                        try
                        {
                            await (await ctx.User.GetOrCreateDMChannelAsync().ConfigureAwait(false))
                                .EmbedAsync(new EmbedBuilder().WithOkColor()
                                .WithTitle(GetText("shop_purchase", ctx.Guild.Name))
                                .AddField(efb => efb.WithName(GetText("item")).WithValue(item.Text).WithIsInline(false))
                                .AddField(efb => efb.WithName(GetText("price")).WithValue(entry.Price.ToString()).WithIsInline(true))
                                .AddField(efb => efb.WithName(GetText("name")).WithValue(entry.Name).WithIsInline(true)))
                                .ConfigureAwait(false);

                            await _cs.AddAsync(entry.AuthorId,
                                    $"Shop sell item - {entry.Name}",
                                    GetProfitAmount(entry.Price)).ConfigureAwait(false);
                        }
                        catch
                        {
                            await _cs.AddAsync(ctx.User.Id,
                                $"Shop error refund - {entry.Name}",
                                entry.Price).ConfigureAwait(false);
                            using (var uow = _db.GetDbContext())
                            {
                                var entries = new IndexedCollection<ShopEntry>(uow.GuildConfigs.ForId(ctx.Guild.Id,
                                    set => set.Include(x => x.ShopEntries)
                                              .ThenInclude(x => x.Items)).ShopEntries);
                                entry = entries.ElementAtOrDefault(index);
                                if (entry != null)
                                {
                                    if (entry.Items.Add(item))
                                    {
                                        await uow.SaveChangesAsync();
                                    }
                                }
                            }
                            await ReplyErrorLocalizedAsync("shop_buy_error").ConfigureAwait(false);
                            return;
                        }
                        await ReplyConfirmLocalizedAsync("shop_item_purchase").ConfigureAwait(false);
                    }
                    else
                    {
                        await ReplyErrorLocalizedAsync("not_enough", Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                        return;
                    }
                }

            }

            private static long GetProfitAmount(int price) =>
                (int)(Math.Ceiling(0.90 * price));

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task ShopAdd(Role _, int price, [Remainder] IRole role)
            {
                var entry = new ShopEntry()
                {
                    Name = "-",
                    Price = price,
                    Type = ShopEntryType.Role,
                    AuthorId = ctx.User.Id,
                    RoleId = role.Id,
                    RoleName = role.Name
                };
                using (var uow = _db.GetDbContext())
                {
                    var entries = new IndexedCollection<ShopEntry>(uow.GuildConfigs.ForId(ctx.Guild.Id,
                        set => set.Include(x => x.ShopEntries)
                                  .ThenInclude(x => x.Items)).ShopEntries)
                    {
                        entry
                    };
                    uow.GuildConfigs.ForId(ctx.Guild.Id, set => set).ShopEntries = entries;
                    await uow.SaveChangesAsync();
                }
                await ctx.Channel.EmbedAsync(EntryToEmbed(entry)
                    .WithTitle(GetText("shop_item_add"))).ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task ShopAdd(List _, int price, [Remainder]string name)
            {
                var entry = new ShopEntry()
                {
                    Name = name.TrimTo(100),
                    Price = price,
                    Type = ShopEntryType.List,
                    AuthorId = ctx.User.Id,
                    Items = new HashSet<ShopEntryItem>(),
                };
                using (var uow = _db.GetDbContext())
                {
                    var entries = new IndexedCollection<ShopEntry>(uow.GuildConfigs.ForId(ctx.Guild.Id,
                        set => set.Include(x => x.ShopEntries)
                                  .ThenInclude(x => x.Items)).ShopEntries)
                    {
                        entry
                    };
                    uow.GuildConfigs.ForId(ctx.Guild.Id, set => set).ShopEntries = entries;
                    await uow.SaveChangesAsync();
                }
                await ctx.Channel.EmbedAsync(EntryToEmbed(entry)
                    .WithTitle(GetText("shop_item_add"))).ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task ShopListAdd(int index, [Remainder] string itemText)
            {
                index -= 1;
                if (index < 0)
                    return;
                var item = new ShopEntryItem()
                {
                    Text = itemText
                };
                ShopEntry entry;
                bool rightType = false;
                bool added = false;
                using (var uow = _db.GetDbContext())
                {
                    var entries = new IndexedCollection<ShopEntry>(uow.GuildConfigs.ForId(ctx.Guild.Id,
                        set => set.Include(x => x.ShopEntries)
                                  .ThenInclude(x => x.Items)).ShopEntries);
                    entry = entries.ElementAtOrDefault(index);
                    if (entry != null && (rightType = (entry.Type == ShopEntryType.List)))
                    {
                        if (added = entry.Items.Add(item))
                        {
                            await uow.SaveChangesAsync();
                        }
                    }
                }
                if (entry == null)
                    await ReplyErrorLocalizedAsync("shop_item_not_found").ConfigureAwait(false);
                else if (!rightType)
                    await ReplyErrorLocalizedAsync("shop_item_wrong_type").ConfigureAwait(false);
                else if (added == false)
                    await ReplyErrorLocalizedAsync("shop_list_item_not_unique").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("shop_list_item_added").ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task ShopRemove(int index)
            {
                index -= 1;
                if (index < 0)
                    return;
                ShopEntry removed;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(ctx.Guild.Id, set => set
                        .Include(x => x.ShopEntries)
                        .ThenInclude(x => x.Items));

                    var entries = new IndexedCollection<ShopEntry>(config.ShopEntries);
                    removed = entries.ElementAtOrDefault(index);
                    if (removed != null)
                    {
                        uow._context.RemoveRange(removed.Items);
                        uow._context.Remove(removed);
                        await uow.SaveChangesAsync();
                    }
                }

                if (removed == null)
                    await ReplyErrorLocalizedAsync("shop_item_not_found").ConfigureAwait(false);
                else
                    await ctx.Channel.EmbedAsync(EntryToEmbed(removed)
                        .WithTitle(GetText("shop_item_rm"))).ConfigureAwait(false);
            }

            public EmbedBuilder EntryToEmbed(ShopEntry entry)
            {
                var embed = new EmbedBuilder().WithOkColor();

                if (entry.Type == ShopEntryType.Role)
                    return embed.AddField(efb => efb.WithName(GetText("name")).WithValue(GetText("shop_role", Format.Bold(ctx.Guild.GetRole(entry.RoleId)?.Name ?? "MISSING_ROLE"))).WithIsInline(true))
                            .AddField(efb => efb.WithName(GetText("price")).WithValue(entry.Price.ToString()).WithIsInline(true))
                            .AddField(efb => efb.WithName(GetText("type")).WithValue(entry.Type.ToString()).WithIsInline(true));
                else if (entry.Type == ShopEntryType.List)
                    return embed.AddField(efb => efb.WithName(GetText("name")).WithValue(entry.Name).WithIsInline(true))
                            .AddField(efb => efb.WithName(GetText("price")).WithValue(entry.Price.ToString()).WithIsInline(true))
                            .AddField(efb => efb.WithName(GetText("type")).WithValue(GetText("random_unique_item")).WithIsInline(true));
                //else if (entry.Type == ShopEntryType.Infinite_List)
                //    return embed.AddField(efb => efb.WithName(GetText("name")).WithValue(GetText("shop_role", Format.Bold(entry.RoleName))).WithIsInline(true))
                //            .AddField(efb => efb.WithName(GetText("price")).WithValue(entry.Price.ToString()).WithIsInline(true))
                //            .AddField(efb => efb.WithName(GetText("type")).WithValue(entry.Type.ToString()).WithIsInline(true));
                else return null;
            }

            public string EntryToString(ShopEntry entry)
            {
                if (entry.Type == ShopEntryType.Role)
                {
                    return GetText("shop_role", Format.Bold(ctx.Guild.GetRole(entry.RoleId)?.Name ?? "MISSING_ROLE"));
                }
                else if (entry.Type == ShopEntryType.List)
                {
                    return GetText("unique_items_left", entry.Items.Count) + "\n" + entry.Name;
                }
                //else if (entry.Type == ShopEntryType.Infinite_List)
                //{

                //}
                return "";
            }
        }
    }
}