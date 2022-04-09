using Discord;
using Discord.WebSocket;
using LinqToDB;
using Mewdeko.Common;
using Mewdeko.Common.Replacements;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Suggestions.Services;

public class SuggestionsService : INService
{
    private readonly DbService _db;
    private readonly PermissionService _perms;
    public readonly DiscordSocketClient Client;
    public readonly AdministrationService Adminserv;
    private readonly Mewdeko _bot;

    public readonly CommandHandler CmdHandler;

    public SuggestionsService(
        DbService db,
        Mewdeko bot,
        CommandHandler cmd,
        DiscordSocketClient client,
        AdministrationService aserv,
        PermissionService permserv)
    {
        _perms = permserv;
        Adminserv = aserv;
        CmdHandler = cmd;
        Client = client;
        Client.MessageReceived += MessageRecieved;
        _db = db;
        _bot = bot;
    }
    

    private Task MessageRecieved(SocketMessage msg)
    {
        _ = Task.Run(async () =>
        {
            if (msg.Channel is not ITextChannel chan)
                return;
            var guild = chan?.Guild;
            var prefix = CmdHandler.GetPrefix(guild);
            if (guild != null && chan.Id == GetSuggestionChannel(guild.Id) && msg.Author.IsBot == false && !msg.Content.StartsWith(prefix))
            {
                if (chan.Id != GetSuggestionChannel(guild.Id))
                    return;
                var guser = msg.Author as IGuildUser;
                var pc = _perms.GetCacheFor(guild.Id);
                var test = pc.Permissions.CheckPermissions(msg as IUserMessage, "suggest", "Suggestions".ToLowerInvariant(), out _);
                if (!test)
                    return;
                if (guser.RoleIds.Contains(Adminserv.GetStaffRole(guser.Guild.Id)))
                    return;
                if (msg.Content.Length > GetMaxLength(guild.Id))
                {
                    try
                    {
                        await msg.DeleteAsync();
                    }
                    catch
                    {
                        // ignore
                    }

                    try
                    {
                        await guser.SendErrorAsync($"Cannot send this suggestion as its over the max length `({GetMaxLength(guild.Id)})` of this server!");
                    }
                    catch
                    {
                        // ignore
                    }

                    return;
                }

                if (msg.Content.Length < GetMinLength(guild.Id))
                {
                    try
                    {
                        await msg.DeleteAsync();
                    }
                    catch
                    {
                        // ignore
                    }

                    try
                    {
                        await guser.SendErrorAsync($"Cannot send this suggestion as its under the minimum length `({GetMaxLength(guild.Id)})` of this server!");
                    }
                    catch
                    {
                        // ignore
                    }

                    return;
                }

                await SendSuggestion(chan.Guild, msg.Author as IGuildUser, Client, msg.Content, msg.Channel as ITextChannel);
                try
                {
                    await msg.DeleteAsync();
                }
                catch
                {
                    //ignored
                }
            }
        });
        return Task.CompletedTask;
    }

    public ulong GetSNum(ulong? id) 
        => _bot.GetGuildConfig(id.Value).sugnum;
    public int GetMaxLength(ulong? id)
        => _bot.GetGuildConfig(id.Value).MaxSuggestLength;
    public int GetMinLength(ulong? id)
        => _bot.GetGuildConfig(id.Value).MinSuggestLength;

    private string GetEmotes(ulong? id)
        => _bot.GetGuildConfig(id.Value).SuggestEmotes;

    public async Task SetButtonType(IGuild guild, int buttonId, int color)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        switch (buttonId)
        {
            case 1:
                gc.Emote1Style = color;
                break;
            case 2:
                gc.Emote2Style = color;
                break;
            case 3:
                gc.Emote3Style = color;
                break;
            case 4:
                gc.Emote4Style = color;
                break;
            case 5:
                gc.Emote5Style = color;
                break;
        }
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }
    
    
    public async Task SetSuggestionEmotes(IGuild guild, string parsedEmotes)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestEmotes = parsedEmotes;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetSuggestionChannelId(IGuild guild, ulong channel)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.sugchan = channel;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }
    public async Task SetMinLength(IGuild guild, int minLength)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.MinSuggestLength = minLength;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }
    public async Task SetMaxLength(IGuild guild, int maxLength)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.MaxSuggestLength = maxLength;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }


    public async Task SetSuggestionMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetAcceptMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.AcceptMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetDenyMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.DenyMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetImplementMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.ImplementMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetSuggestThreadsType(IGuild guild, int num)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.SuggestionThreadType = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetConsiderMessage(IGuild guild, string message)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.ConsiderMessage = message;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task Sugnum(IGuild guild, ulong num)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.sugnum = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }
    
    public async Task SetEmoteMode(IGuild guild, int mode)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.EmoteMode = mode;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task UpdateEmoteCount(ulong messageId, int emoteNumber, bool negative = false)
    {
        ulong count;
        await using var uow = _db.GetDbContext();
        var suggest = uow.Suggestions.FirstOrDefault(x => x.MessageID == messageId);
        uow.Suggestions.Remove(suggest);
        await uow.SaveChangesAsync();
        if (!negative)
            count = emoteNumber switch
        {
            1 => ++suggest.EmoteCount1,
            2 => ++suggest.EmoteCount2,
            3 => ++suggest.EmoteCount3,
            4 => ++suggest.EmoteCount4,
            5 => ++suggest.EmoteCount5,
            _ => 0
        };
        else
            count = emoteNumber switch
            {
                1 => --suggest.EmoteCount1,
                2 => --suggest.EmoteCount2,
                3 => --suggest.EmoteCount3,
                4 => --suggest.EmoteCount4,
                5 => --suggest.EmoteCount5,
                _ => 0
            };
        uow.Suggestions.Add(suggest);
        await uow.SaveChangesAsync();
    }

    public async Task<ulong> GetCurrentCount(IGuild guild, ulong messageId, int emoteNumber)
    {
        ulong count;
        await using var uow = _db.GetDbContext();
        var toupdate = uow.Suggestions.FirstOrDefault(x => x.MessageID == messageId);
        count = emoteNumber switch
        {
            1 => toupdate.EmoteCount1,
            2 => toupdate.EmoteCount2,
            3 => toupdate.EmoteCount3,
            4 => toupdate.EmoteCount4,
            5 => toupdate.EmoteCount5,
            _ => 0
        };
        return count;
    }

    public IEmote GetSuggestMote(IGuild guild, int num)
    {
        var tup = new Emoji("\uD83D\uDC4D");
        var tdown = new Emoji("\uD83D\uDC4E");
        var emotes = _bot.GetGuildConfig(guild.Id).SuggestEmotes;
        if (emotes is null or "disabled")
        {
            return num == 1 ? tup : tdown;
        }

        return emotes.Split(",")[num-1].ToIEmote();
    }
    public ulong GetSuggestionChannel(ulong? id) => _bot.GetGuildConfig(id.Value).sugchan;

    public string GetSuggestionMessage(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).SuggestMessage;

    public string GetAcceptMessage(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).AcceptMessage;

    public string GetDenyMessage(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).DenyMessage;

    public string GetImplementMessage(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).ImplementMessage;

    public string GetConsiderMessage(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).ConsiderMessage;
    
    
    public int GetThreadType(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).SuggestionThreadType;
    
    public int GetEmoteMode(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).EmoteMode;
    
    public ulong GetConsiderChannel(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).ConsiderChannel;
    
    public ulong GetAcceptChannel(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).AcceptChannel;
    
    public ulong GetImplementChannel(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).ImplementChannel;
    
    public ulong GetDenyChannel(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).DenyChannel;
    
    public bool GetArchiveOnDeny(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).ArchiveOnDeny;
    
    public bool GetArchiveOnAccept(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).ArchiveOnAccept;
    
    public bool GetArchiveOnConsider(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).ArchiveOnConsider;
    
    public bool GetArchiveOnImplement(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).ArchiveOnImplement;
    
    public string GetSuggestButtonName(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).SuggestButtonName;
    
    public ulong GetSuggestButtonChannel(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).SuggestButtonChannel;
    
    public string GetSuggestButtonEmote(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).SuggestButtonEmote;
    
    public string GetSuggestButtonMessage(IGuild guild)
        => _bot.GetGuildConfig(guild.Id).SuggestButtonMessage;

    public ButtonStyle GetButtonStyle(IGuild guild, int id) =>
        id switch
        {
            1 => (ButtonStyle)_bot.GetGuildConfig(guild.Id).Emote1Style,
            2 => (ButtonStyle)_bot.GetGuildConfig(guild.Id).Emote2Style,
            3 => (ButtonStyle)_bot.GetGuildConfig(guild.Id).Emote3Style,
            4 => (ButtonStyle)_bot.GetGuildConfig(guild.Id).Emote4Style,
            5 => (ButtonStyle)_bot.GetGuildConfig(guild.Id).Emote5Style,
            _ => ButtonStyle.Secondary
        };


    public async Task SendDenyEmbed(IGuild guild, DiscordSocketClient client, IUser user, ulong suggestion,
        ITextChannel channel, string? reason = null, IDiscordInteraction? interaction = null)
    {
        string rs;
        rs = reason ?? "none";
        var suggest = Suggestions(guild.Id, suggestion).FirstOrDefault();
        if (suggest is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.");
                return;
            }

            await interaction.SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.");
            return;
        }
        var use = await guild.GetUserAsync(suggest.UserID);
        EmbedBuilder eb;
        if (GetDenyMessage(guild) is "-" or "" or null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder()
                    .WithAuthor(use)
                    .WithTitle($"Suggestion #{suggestion} Denied")
                    .WithDescription(suggest.Suggestion)
                    .WithOkColor()
                    .AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)))
                    .GetMessageAsync(suggest.MessageID);
                eb = new EmbedBuilder()
                    .WithAuthor(use)
                    .WithTitle($"Suggestion #{suggestion} Denied")
                    .WithDescription(desc.Embeds.FirstOrDefault()?.Description)
                    .WithOkColor()
                    .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
            try
            {
                await message.RemoveAllReactionsAsync();
            }
            catch
            {
                // ignored
            }

            await message.ModifyAsync(x =>
            {
                x.Content = null;
                x.Embed = eb.Build();
            });
            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Denied");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Denied By", user);
                emb.WithErrorColor();
                await (await guild.GetUserAsync(suggest.UserID)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as denied and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as denied and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as denied but the user had their DMs off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as denied but the user had DMs off.");
            }
        }
        else
        {
            string sug;
            if (suggest.Suggestion == null)
                sug = (await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)))
                    .GetMessageAsync(suggest.MessageID)).Embeds.FirstOrDefault()
                    ?.Description;
            else
                sug = suggest.Suggestion;
            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
            var suguse = await guild.GetUserAsync(suggest.UserID);
            var replacer = new ReplacementBuilder()
                .WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", () => suguse.ToString())
                .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                .WithOverride("%suggest.number%", () => suggest.SuggestID.ToString())
                .WithOverride("%suggest.user.name%", () => suguse.Username)
                .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                .WithOverride("%suggest.mod.user%", () => user.ToString())
                .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                .WithOverride("%suggest.mod.name%", () => user.Username)
                .WithOverride("%suggest.mod.message%", () => rs)
                .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString())
                .Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(GetDenyMessage(guild)), out var embed, out var plainText);
            if (ebe is false)
                await message.ModifyAsync(x =>
                {
                    x.Embed = null;
                    x.Content = replacer.Replace(GetDenyMessage(guild));
                });
            else
                await message.ModifyAsync(x =>
                {
                    x.Content = plainText;
                    x.Embed = embed?.Build();
                });

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Denied");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Denied By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserID)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as denied and the user has been dmed the denial!");
                else
                    await interaction.SendConfirmAsync("Suggestion set as denied and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as denied but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as denied but the user had DMs off.");
            }
        }
    }

    public async Task SendConsiderEmbed(IGuild guild, DiscordSocketClient client, IUser user, ulong suggestion,
        ITextChannel channel, string? reason = null, IDiscordInteraction? interaction = null)
    {
        string rs;
        if (reason == null)
            rs = "none";
        else
            rs = reason;
        var suggest = Suggestions(guild.Id, suggestion).FirstOrDefault();
        if (suggest.Suggestion is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.");
                return;
            }

            await interaction.SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.");
            return;
        }
        var use = await guild.GetUserAsync(suggest.UserID);
        EmbedBuilder eb;
        if (GetConsiderMessage(guild) is "-" or "" or
            null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder()
                    .WithAuthor(use)
                    .WithTitle($"Suggestion #{suggestion} Considering")
                    .WithDescription(suggest.Suggestion)
                    .WithOkColor()
                    .AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)))
                    .GetMessageAsync(suggest.MessageID);
                eb = new EmbedBuilder()
                    .WithAuthor(use)
                    .WithTitle($"Suggestion #{suggestion} Considering")
                    .WithDescription(desc.Embeds.FirstOrDefault().Description)
                    .WithOkColor()
                    .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
            try
            {
                await message.RemoveAllReactionsAsync();
            }
            catch
            {
                // ignored
            }

            await message.ModifyAsync(x =>
            {
                x.Content = null;
                x.Embed = eb.Build();
            });
            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Considering");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Denied By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserID)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered but the user had DMs off.");
            }
        }
        else
        {
            string sug;
            if (suggest.Suggestion == null)
                sug = (await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)))
                           .GetMessageAsync(suggest.MessageID)).Embeds.FirstOrDefault()!.Description;
            else
                sug = suggest.Suggestion;
            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
            var suguse = await guild.GetUserAsync(suggest.UserID);
            var replacer = new ReplacementBuilder()
                           .WithServer(client, guild as SocketGuild)
                           .WithOverride("%suggest.user%", () => suguse.ToString())
                           .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                           .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                           .WithOverride("%suggest.number%", () => suggest.SuggestID.ToString())
                           .WithOverride("%suggest.user.name%", () => suguse.Username)
                           .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                           .WithOverride("%suggest.mod.user%", () => user.ToString())
                           .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                           .WithOverride("%suggest.mod.name%", () => user.Username)
                           .WithOverride("%suggest.mod.message%", () => rs)
                           .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                           .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                           .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                           .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                           .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                           .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString())
                           .Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(GetConsiderMessage(guild)), out var embed, out var plainText);
            if (ebe is false)
                await message.ModifyAsync(x =>
                {
                    x.Embed = null;
                    x.Content = replacer.Replace(GetConsiderMessage(guild));
                });
            else
                await message.ModifyAsync(x =>
                {
                    x.Content = plainText;
                    x.Embed = embed?.Build();
                });
            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Considering");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Considered by", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserID)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as considered but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as considered but the user had DMs off.");
            }
        }
    }

    public async Task SendImplementEmbed(IGuild guild, DiscordSocketClient client, IUser user, ulong suggestion,
        ITextChannel channel, string? reason = null, IDiscordInteraction? interaction = null)
    {
        string rs;
        if (reason == null)
            rs = "none";
        else
            rs = reason;
        var suggest = Suggestions(guild.Id, suggestion).FirstOrDefault();
        if (suggest.Suggestion is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.");
                return;
            }

            await interaction.SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.");
            return;
        }
        var use = await guild.GetUserAsync(suggest.UserID);
        EmbedBuilder eb;
        if (GetImplementMessage(guild) is "-" or "" or
            null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder()
                    .WithAuthor(use)
                    .WithTitle($"Suggestion #{suggestion} Implemented")
                    .WithDescription(suggest.Suggestion)
                    .WithOkColor()
                    .AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)))
                    .GetMessageAsync(suggest.MessageID);
                eb = new EmbedBuilder()
                    .WithAuthor(use)
                    .WithTitle($"Suggestion #{suggestion} Implemented")
                    .WithDescription(desc.Embeds.FirstOrDefault().Description)
                    .WithOkColor()
                    .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
            await message.ModifyAsync(x =>
            {
                x.Content = null;
                x.Embed = eb.Build();
            });
            try
            {
                await message.RemoveAllReactionsAsync();
            }
            catch
            {
                // ignored
            }

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Implemented");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Implemented By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserID)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented but the user had DMs off.");

            }
        }
        else
        {
            string sug;
            if (suggest.Suggestion == null)
                sug = (await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)))
                    .GetMessageAsync(suggest.MessageID)).Embeds.FirstOrDefault()
                    ?.Description;
            else
                sug = suggest.Suggestion;
            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
            GetSNum(guild.Id);
            var suguse = await guild.GetUserAsync(suggest.UserID);
            var replacer = new ReplacementBuilder()
                           .WithServer(client, guild as SocketGuild)
                           .WithOverride("%suggest.user%", () => suguse.ToString())
                           .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                           .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                           .WithOverride("%suggest.number%", () => suggest.SuggestID.ToString())
                           .WithOverride("%suggest.user.name%", () => suguse.Username)
                           .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                           .WithOverride("%suggest.mod.user%", () => user.ToString())
                           .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                           .WithOverride("%suggest.mod.name%", () => user.Username)
                           .WithOverride("%suggest.mod.message%", () => rs)
                           .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                           .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                           .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                           .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                           .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                           .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString())
                           .Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(GetImplementMessage(guild)), out var embed, out var plainText);
            if (ebe is false)
                await message.ModifyAsync(x =>
                {
                    x.Embed = null;
                    x.Content = replacer.Replace(GetImplementMessage(guild));
                });
            else
                await message.ModifyAsync(x =>
                {
                    x.Content = plainText;
                    x.Embed = embed?.Build();
                });

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Implemented");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Implemented By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserID)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as implemented but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as implemented but the user had DMs off.");

            }
        }
    }

    public async Task SendAcceptEmbed(IGuild guild, DiscordSocketClient client, IUser user, ulong suggestion,
        ITextChannel channel, string? reason = null, IDiscordInteraction? interaction = null)
    {
        var rs = reason ?? "none";
        var suggest = Suggestions(guild.Id, suggestion).FirstOrDefault();
        if (suggest.Suggestion is null)
        {
            if (interaction is null)
            {
                await channel.SendErrorAsync("That suggestion wasn't found! Please check the number and try again.");
                return;
            }

            await interaction.SendEphemeralErrorAsync("That suggestion wasn't found! Please check the number and try again.");
            return;
        }
        var use = await guild.GetUserAsync(suggest.UserID);
        EmbedBuilder eb;
        if (GetAcceptMessage(guild) is "-" or "" or null)
        {
            if (suggest.Suggestion != null)
            {
                eb = new EmbedBuilder()
                    .WithAuthor(use)
                    .WithTitle($"Suggestion #{suggestion} Accepted")
                    .WithDescription(suggest.Suggestion)
                    .WithOkColor()
                    .AddField("Reason", rs);
            }
            else
            {
                var desc = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)))
                    .GetMessageAsync(suggest.MessageID);
                eb = new EmbedBuilder()
                    .WithAuthor(use)
                    .WithTitle($"Suggestion #{suggestion} Accepted")
                    .WithDescription(desc.Embeds.FirstOrDefault().Description)
                    .WithOkColor()
                    .AddField("Reason", rs);
            }

            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
            await message.ModifyAsync(x =>
            {
                x.Content = null;
                x.Embed = eb.Build();
            });
            try
            {
                await message.RemoveAllReactionsAsync();
            }
            catch
            {
                // ignored
            }

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Accepted");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Accepted By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserID)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted but the user had DMs off.");

            }
        }
        else
        {
            string sug;
            if (suggest.Suggestion is null)
                sug = (await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id)))
                    .GetMessageAsync(suggest.MessageID)).Embeds.FirstOrDefault().Description;
            else
                sug = suggest.Suggestion;
            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            var message = await chan.GetMessageAsync(suggest.MessageID) as IUserMessage;
            GetSNum(guild.Id);
            var suguse = await guild.GetUserAsync(suggest.UserID);
            var replacer = new ReplacementBuilder()
                           .WithServer(client, guild as SocketGuild)
                           .WithOverride("%suggest.user%", () => suguse.ToString())
                           .WithOverride("%suggest.user.id%", () => suguse.Id.ToString())
                           .WithOverride("%suggest.message%", () => sug.SanitizeMentions(true))
                           .WithOverride("%suggest.number%", () => suggest.SuggestID.ToString())
                           .WithOverride("%suggest.user.name%", () => suguse.Username)
                           .WithOverride("%suggest.user.avatar%", () => suguse.RealAvatarUrl().ToString())
                           .WithOverride("%suggest.mod.user%", () => user.ToString())
                           .WithOverride("%suggest.mod.avatar%", () => user.RealAvatarUrl().ToString())
                           .WithOverride("%suggest.mod.name%", () => user.Username)
                           .WithOverride("%suggest.mod.message%", () => rs)
                           .WithOverride("%suggest.mod.Id%", () => user.Id.ToString())
                           .WithOverride("%suggest.emote1count%", () => suggest.EmoteCount1.ToString())
                           .WithOverride("%suggest.emote2count%", () => suggest.EmoteCount2.ToString())
                           .WithOverride("%suggest.emote3count%", () => suggest.EmoteCount3.ToString())
                           .WithOverride("%suggest.emote4count%", () => suggest.EmoteCount4.ToString())
                           .WithOverride("%suggest.emote5count%", () => suggest.EmoteCount5.ToString())
                           .Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(GetAcceptMessage(guild)), out var embed, out var plainText);
            if (ebe is false)
                await message.ModifyAsync(x =>
                {
                    x.Embed = null;
                    x.Content = replacer.Replace(GetAcceptMessage(guild));
                });
            else
                await message.ModifyAsync(x =>
                {
                    x.Content = plainText;
                    x.Embed = embed?.Build();
                });

            try
            {
                var emb = new EmbedBuilder();
                emb.WithAuthor(use);
                emb.WithTitle($"Suggestion #{suggestion} Accepted");
                emb.WithDescription(suggest.Suggestion);
                emb.AddField("Reason", rs);
                emb.AddField("Accepted By", user);
                emb.WithOkColor();
                await (await guild.GetUserAsync(suggest.UserID)).SendMessageAsync(embed: emb.Build());
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted and the user has been dmed.");
            }
            catch
            {
                if (interaction is null)
                    await channel.SendConfirmAsync("Suggestion set as accepted but the user had their dms off.");
                else
                    await interaction.SendConfirmAsync("Suggestion set as accepted but the user had DMs off.");
            }
        }
    }

    public async Task SendSuggestion(IGuild guild, IGuildUser user, DiscordSocketClient client, string suggestion,
        ITextChannel channel, IDiscordInteraction? interaction = null)
    {
        if (GetSuggestionChannel(guild.Id) == 0)
        {   
            if (interaction is null)
            {
                var msg = await channel.SendErrorAsync(
                "There is no suggestion channel set! Have an admin set it using `setsuggestchannel` and try again!");
                msg.DeleteAfter(3);
                return;
            }

            await interaction.SendEphemeralErrorAsync(
                "There is no suggestion channel set! Have an admin set it using `setsuggestchannel` then try again!");
            return;
        }

        var tup = new Emoji("\uD83D\uDC4D");
        var tdown = new Emoji("\uD83D\uDC4E");
        var emotes = new List<Emote>();
        var em = GetEmotes(guild.Id);
        if (em is not null and not "disable")
        {
            var te = em.Split(",");
            emotes.AddRange(te.Select(Emote.Parse));
        }
        var builder = new ComponentBuilder();
        IEmote[] reacts = { tup, tdown };
        if (GetEmoteMode(guild) == 1)
        {
            var count = 0;
            if (em is null or "disabled")
                foreach (var i in reacts)
                {
                    builder.WithButton("0", $"emotebutton:{count+1}", emote: i, style: GetButtonStyle(guild, ++count));
                }
            else
                foreach (var i in emotes)
                {
                    builder.WithButton("0", $"emotebutton:{count+1}", emote: i, style: GetButtonStyle(guild, ++count));
                }
        }

        if (GetThreadType(guild) == 1)
        {
            builder.WithButton("Join/Create Public Thread", customId: $"publicsuggestthread:{GetSNum(guild.Id)}", ButtonStyle.Secondary, row: 1);
        }
        if (GetThreadType(guild) == 2)
        {
            builder.WithButton("Join/Create Private Thread", customId: $"privatesuggestthread:{GetSNum(guild.Id)}", ButtonStyle.Secondary, row: 1);
        }
        if (GetSuggestionMessage(guild) is "-" or "")
        {
            var sugnum1 = GetSNum(guild.Id);
            var t = await (await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id))).SendMessageAsync(embed:
                new EmbedBuilder()
                    .WithAuthor(user)
                    .WithTitle($"Suggestion #{GetSNum(guild.Id)}")
                    .WithDescription(suggestion)
                    .WithOkColor().Build(), components: builder.Build());
            if (GetEmoteMode(guild) == 0)
            {
                if (em is null or "disabled")
                    foreach (var i in reacts)
                        await t.AddReactionAsync(i);
                else
                    foreach (var ei in emotes)
                        await t.AddReactionAsync(ei);
            }
            
            
            await Sugnum(guild, sugnum1 + 1);
            await Suggest(guild, sugnum1, t.Id, user.Id, suggestion);
            if (interaction is not null)
                await interaction.SendEphemeralConfirmAsync("Suggestion has been sent!");
        }
        else
        {
            var sugnum1 = GetSNum(guild.Id);
            var replacer = new ReplacementBuilder()
                .WithServer(client, guild as SocketGuild)
                .WithOverride("%suggest.user%", user.ToString)
                .WithOverride("%suggest.message%", () => suggestion.SanitizeMentions(true))
                .WithOverride("%suggest.number%", () => sugnum1.ToString())
                .WithOverride("%suggest.user.name%", () => user.Username)
                .WithOverride("%suggest.user.avatar%", () => user.RealAvatarUrl().ToString())
                .Build();
            var ebe = SmartEmbed.TryParse(replacer.Replace(GetSuggestionMessage(guild)), out var embed, out var plainText);
            var chan = await guild.GetTextChannelAsync(GetSuggestionChannel(guild.Id));
            IUserMessage msg = null;
            if (ebe is false)
            {
                if (GetEmoteMode(guild) == 1)
                    msg = await chan.SendMessageAsync(replacer.Replace(GetSuggestionMessage(guild)), components: builder.Build());
                else
                    msg = await chan.SendMessageAsync(replacer.Replace(GetSuggestionMessage(guild)));
            }
            else
            {
                if (GetEmoteMode(guild) == 1)
                    msg = await chan.SendMessageAsync(plainText, embed: embed?.Build(), components: builder.Build());
                else
                    msg = await chan.SendMessageAsync(plainText, embed: embed?.Build());
            }

            if (GetEmoteMode(guild) == 0)
            {
                if (em is null or "disabled" or "-")
                    foreach (var i in reacts)
                        await msg.AddReactionAsync(i);
                else
                    foreach (var ei in emotes)
                        await msg.AddReactionAsync(ei);
            }
            await Sugnum(guild, sugnum1 + 1);
            await Suggest(guild, sugnum1, msg.Id, user.Id, suggestion);

            if (interaction is not null)
                await interaction.SendEphemeralConfirmAsync("Suggestion has been sent!");
            else
                await channel.SendConfirmAsync("Suggestion sent!");
        }
    }

    public async Task Suggest(IGuild guild, ulong suggestId, ulong messageId, ulong userId, string suggestion)
    {
        var guildId = guild.Id;

        var suggest = new SuggestionsModel
        {
            GuildId = guildId,
            SuggestID = suggestId,
            MessageID = messageId,
            UserID = userId,
            Suggestion = suggestion
        };
        await using var uow = _db.GetDbContext();
        uow.Suggestions.Add(suggest);

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public SuggestionsModel[] Suggestions(ulong gid, ulong sid)
    {
        using var uow = _db.GetDbContext();
        return uow.Suggestions.ForId(gid, sid);
    }

    public SuggestionsModel GetSuggestByMessage(ulong msgId)
    {
        using var uow = _db.GetDbContext();
        return uow.Suggestions.FirstOrDefault(x => x.MessageID == msgId);
    }

    public SuggestionsModel[] ForUser(ulong guildId, ulong userId)
    {
        using var uow = _db.GetDbContext();
        return uow.Suggestions.ForUser(guildId, userId);
    }

    public int GetPickedEmote(ulong messageId, ulong userId)
    {
        using var uow = _db.GetDbContext();
        var toreturn = uow.SuggestVotes.FirstOrDefault(x => x.UserId == userId && x.MessageId == messageId);
        return toreturn?.EmotePicked ?? 0;
    }

    public async Task UpdatePickedEmote(ulong messageId, ulong userId, int emotePicked)
    {
        await using var uow = _db.GetDbContext();
        var tocheck = uow.SuggestVotes.FirstOrDefault(x => x.MessageId == messageId && x.UserId == userId);
        if (tocheck is null)
        {
            var toadd = new SuggestVotes() { EmotePicked = emotePicked, MessageId = messageId, UserId = userId };
            uow.SuggestVotes.Add(toadd);
            await uow.SaveChangesAsync();
        }
        else
        {
            tocheck.EmotePicked = emotePicked;
            uow.SuggestVotes.Update(tocheck);
            await uow.SaveChangesAsync();
        }
    }

    public async Task AddThreadChannel(ulong messageId, ulong threadChannelId)
    {
        await using var uow = _db.GetDbContext();
        uow.SuggestThreads.Add(new SuggestThreads()
        {
            MessageId = messageId,
            ThreadChannelId = threadChannelId
        });
        await uow.SaveChangesAsync();
    }

    public ulong GetThreadByMessage(ulong messageId)
    {
        using var uow = _db.GetDbContext();
        return uow.SuggestThreads.FirstOrDefault(x => x.MessageId == messageId)?.ThreadChannelId ?? 0;
    }
}