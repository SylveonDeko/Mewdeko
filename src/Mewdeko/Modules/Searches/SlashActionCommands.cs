using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using NekosBestApiNet;
using NekosBestApiNet.Models.Images;

namespace Mewdeko.Modules.Searches;

/// <summary>
/// Submodule containing action commands.
/// </summary>
[Group("rp", "Different actions like hug, kiss, etc")]
public class SlashActionCommands(NekosBestApi nekosBestApi) : MewdekoSlashCommandModule
{
    /// <summary>
    /// Shoots someone.
    /// </summary>
    /// <param name="toShow">The person to shoot.</param>
    /// <example>
    /// .shoot @user
    /// </example>
    [SlashCommand("shoot", "shoot someone")]
    public async Task Shoot(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Shoot(), "shot", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Holds someone's hand.
    /// </summary>
    /// <param name="toShow">The person to hold hands with.</param>
    /// <example>
    /// .handhold @user
    /// </example>
    [SlashCommand("handhold", "Hold someones hand")]
    public async Task Handhold(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Handhold(), "handholds", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Punches someone.
    /// </summary>
    /// <param name="toShow">The person to punch.</param>
    /// <example>
    /// .punch @user
    /// </example>
    [SlashCommand("punch", "Punch someone")]
    public async Task Punch(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Punch(), "punched", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Hugs someone.
    /// </summary>
    /// <param name="toShow">The person to hug.</param>
    /// <example>
    /// .hug @user
    /// </example>
    [SlashCommand("hug", "Hug someone")]
    public async Task Hug(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Hug(), "hugs", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Kisses someone.
    /// </summary>
    /// <param name="toShow">The person to kiss.</param>
    /// <example>
    /// .kiss @user
    /// </example>
    [SlashCommand("kiss", "Kiss someone")]
    public async Task Kiss(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Kiss(), "Kissed", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Pats someone.
    /// </summary>
    /// <param name="toShow">The person to pat.</param>
    /// <example>
    /// .pat @user
    /// </example>
    [SlashCommand("pat", "Headpat someone")]
    public async Task Pat(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Pat(), "gave pattus to", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Tickles someone.
    /// </summary>
    /// <param name="toShow">The person to tickle.</param>
    /// <example>
    /// .tickle @user
    /// </example>
    [SlashCommand("tickle", "tickle someone")]
    public async Task Tickle(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Tickle(), "tickles", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Slaps someone.
    /// </summary>
    /// <param name="toShow">The person to slap.</param>
    /// <example>
    /// .slap @user
    /// </example>
    [SlashCommand("slap", "slap someone")]
    public async Task Slap(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Slap(), "slapped", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Cuddles with someone.
    /// </summary>
    /// <param name="toShow">The person to cuddle with.</param>
    /// <example>
    /// .cuddle @user
    /// </example>
    [SlashCommand("cuddle", "Cuddle someone")]
    public async Task Cuddle(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Cuddle(), "cuddles with", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Pokes someone.
    /// </summary>
    /// <param name="toShow">The person to poke.</param>
    /// <example>
    /// .poke @user
    /// </example>
    [SlashCommand("poke", "Poke someone")]
    public async Task Poke(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Poke(), "poked", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Feeds someone.
    /// </summary>
    /// <param name="toShow">The person to feed.</param>
    /// <example>
    /// .feed @user
    /// </example>
    [SlashCommand("feed", "Feed someone")]
    public async Task Feed(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Feed(), "is feeding", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Calls someone a baka.
    /// </summary>
    /// <param name="toShow">The person to call baka.</param>
    /// <example>
    /// .baka @user
    /// </example>
    [SlashCommand("baka", "Call someone an idiot")]
    public async Task Baka(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Baka(), "bakas", toShow)
            .ConfigureAwait(false);

    private async Task SendAction(ActionResult result, string action, string toShow = null)
    {
        var em = new EmbedBuilder
        {
            Description =
                toShow is null
                    ? $"{ctx.User.Mention} is {action}\n{toShow}"
                    : $"{ctx.User.Mention} {action} {toShow}",
            ImageUrl = result.Results.FirstOrDefault().Url,
            Color = Mewdeko.OkColor
        };
        await ctx.Interaction.RespondAsync(embed: em.Build()).ConfigureAwait(false);
    }
}
/// <summary>
/// Submodule containing action commands.
/// </summary>
[Group("rp2", "Different actions like hug, kiss, etc")]
public class SlashActionCommandsTwo(NekosBestApi nekosBestApi) : MewdekoSlashCommandModule
{
    /// <summary>
    /// Bites someone.
    /// </summary>
    /// <param name="toShow">The person to bite.</param>
    /// <example>
    /// .bite @user
    /// </example>
    [SlashCommand("bite", "Bite someone")]
    public async Task Bite(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Bite(), "bites", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Blushes.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .blush
    /// </example>
    [SlashCommand("blush", "Makes you blush")]
    public async Task Blush(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Blush(), "blushes", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Indicates boredom.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .bored
    /// </example>
    [SlashCommand("bored", "Show that you're bored")]
    public async Task Bored(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Bored(), "is bored", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Cries.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .cry
    /// </example>
    [SlashCommand("cry", "cry")]
    public async Task Cry(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Cry(), "cries", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Dances.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .dance
    /// </example>
    [SlashCommand("dance", "dance")]
    public async Task Dance(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Dance(), "dances", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Facepalms.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .facepalm
    /// </example>
    [SlashCommand("facepalm", "*facepalm*")]
    public async Task Facepalm(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Facepalm(), "facepalms", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Expresses happiness.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .happy
    /// </example>
    [SlashCommand("happy", ":3")]
    public async Task Happy(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Happy(), "is happy", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Gives someone a high-five.
    /// </summary>
    /// <param name="toShow">The person to give a high-five.</param>
    /// <example>
    /// .highfive @user
    /// </example>
    [SlashCommand("highfive", "High five someone")]
    public async Task HighFive(string toShow) =>
        await SendAction(await nekosBestApi.ActionsApi.Highfive(), "high-fives", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Laughs.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .laugh
    /// </example>
    [SlashCommand("laugh", "laugh for some reason on this god forsaken platform")]
    public async Task Laugh(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Laugh(), "laughs", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Pouts.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .pout
    /// </example>
    [SlashCommand("pout", "pout")]
    public async Task Pout(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Pout(), "pouts", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Shrugs.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .shrug
    /// </example>
    [SlashCommand("shrug", "shrug")]
    public async Task Shrug(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Shrug(), "shrugs", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Sleeps.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .sleep
    /// </example>
    [SlashCommand("sleep", "nini")]
    public async Task Sleep(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Sleep(), "sleeps", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Smiles.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .smile
    /// </example>
    [SlashCommand("smile", "smile")]
    public async Task Smile(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Smile(), "smiles", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Is smug.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .smug
    /// </example>
    [SlashCommand("smug", "hehe")]
    public async Task Smug(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Smug(), "is smug", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Stares.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .stare
    /// </example>
    [SlashCommand("stare", "jiiiiiiii")]
    public async Task Stare(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Stare(), "stares", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Thinks.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .think
    /// </example>
    [SlashCommand("think", "thonk")]
    public async Task Think(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Think(), "thinks", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Gives a thumbs-up.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .thumbsup
    /// </example>
    [SlashCommand("thumbsup", "thumbs up")]
    public async Task ThumbsUp(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Thumbsup(), "gives a thumbs-up", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Waves.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .wave
    /// </example>
    [SlashCommand("wave", "waves")]
    public async Task Wave(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Wave(), "waves", toShow)
            .ConfigureAwait(false);

    /// <summary>
    /// Winks.
    /// </summary>
    /// <param name="toShow">Additional text (optional).</param>
    /// <example>
    /// .wink
    /// </example>
    [SlashCommand("wink", "- ^ 0")]
    public async Task Wink(string toShow = null) =>
        await SendAction(await nekosBestApi.ActionsApi.Wink(), "winks", toShow)
            .ConfigureAwait(false);

    private async Task SendAction(ActionResult result, string action, string toShow = null)
    {
        var em = new EmbedBuilder
        {
            Description =
                toShow is null
                    ? $"{ctx.User.Mention} is {action}\n{toShow}"
                    : $"{ctx.User.Mention} {action} {toShow}",
            ImageUrl = result.Results.FirstOrDefault().Url,
            Color = Mewdeko.OkColor
        };
        await ctx.Interaction.RespondAsync(embed: em.Build()).ConfigureAwait(false);
    }
}