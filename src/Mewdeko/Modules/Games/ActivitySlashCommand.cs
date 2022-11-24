using Discord.Interactions;
using Mewdeko.Modules.Games.Services;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes.InteractionCommands;

namespace Mewdeko.Modules.Games;
[Group("games", "Some of mewdekos games!")]
public class ActivitySlashCommand : MewdekoSlashSubmodule<ActivityService>
{
    [SlashCommand("activity", "Launch a discord activity in a voice channel!"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Activity(IVoiceChannel chan, DefaultApplications app)
    {
        var eb = new EmbedBuilder().WithOkColor();
        var gmrole = await Service.GetGameMasterRole(ctx.Guild.Id);
        if (gmrole != 0 && !((IGuildUser)ctx.User).RoleIds.Contains(gmrole))
        {
            await ctx.Interaction.RespondAsync(embed: eb.WithDescription("You are not a Game Master!").WithErrorColor().Build(), ephemeral: true);
            return;
        }
        var invite = await chan.CreateInviteToApplicationAsync(app);
        await ctx.Interaction.RespondAsync(embed: eb.WithDescription($"[Click here to join the vc and start {app.ToString()}]({invite.Url})").Build());
    }

    [SlashCommand("setgamemasterrole", "Allows you to set the game master role"),
     RequireUserPermission(GuildPermission.ManageGuild), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task SetGameMaster(IRole? role = null)
    {
        var eb = new EmbedBuilder().WithOkColor();
        if (role is null)
        {
            await Service.GameMasterRoleSet(ctx.Guild.Id, 0);
            await ctx.Interaction.RespondAsync(embed: eb.WithDescription("Game Master Role Disabled.").Build());
            return;
        }
        await Service.GameMasterRoleSet(ctx.Guild.Id, role.Id);
        await ctx.Interaction.RespondAsync(embed: eb.WithDescription("Successfully set the Game Master Role!").Build());
    }
}