using Discord;
using Discord.Interactions;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public class ActivitySlashCommand : MewdekoSlashSubmodule<ActivityService>
{
    [SlashCommand("activity", "Launch a discord activity in a voice channel!"), RequireContext(ContextType.Guild)]
    public async Task Activity(IVoiceChannel chan, DefaultApplications app)
    {
        var eb = new EmbedBuilder().WithOkColor();
        var gmrole = await Service.GetGameMasterRole(Ctx.Guild.Id);
        if (gmrole != 0 && !((IGuildUser)Ctx.User).RoleIds.Contains(gmrole))
        {
            await Ctx.Interaction.RespondAsync(embed: eb.WithDescription("You are not a Game Master!").WithErrorColor().Build(), ephemeral: true);
            return;
        }
        var invite = await chan.CreateInviteToApplicationAsync(app);
        await Ctx.Interaction.RespondAsync(embed: eb.WithDescription($"[Click here to join the vc and start {app.ToString()}]({invite.Url})").Build());
    }

    [SlashCommand("setgamemasterrole", "Allows you to set the game master role"),
     RequireUserPermission(GuildPermission.ManageGuild), RequireContext(ContextType.Guild)]
    public async Task SetGameMaster(IRole role = null)
    {
        var eb = new EmbedBuilder().WithOkColor();
        if (role is null)
        {
            await Service.GameMasterRoleSet(Ctx.Guild.Id, 0);
            await Ctx.Interaction.RespondAsync(embed: eb.WithDescription("Game Master Role Disabled.").Build());
            return;
        }
        await Service.GameMasterRoleSet(Ctx.Guild.Id, role.Id);
        await Ctx.Interaction.RespondAsync(embed: eb.WithDescription("Successfully set the Game Master Role!").Build());
    }
}