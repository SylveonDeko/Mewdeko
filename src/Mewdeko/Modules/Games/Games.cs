using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Services;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Games;
public partial class Games : MewdekoModuleBase<GamesService>
{
    private readonly IImageCache _images;
    private readonly Random _rng = new();
    private readonly MewdekoContext _db;

    public Games(IDataCache data, DbService db) => (_images, _db) = (data.LocalImages, db.GetDbContext());

    [Cmd, Aliases]
    public async Task Choose([Remainder] string? list = null)
    {
        if (string.IsNullOrWhiteSpace(list))
            return;
        var listArr = list.Split(';');
        if (listArr.Length < 2)
            return;
        var rng = new MewdekoRandom();
        await ctx.Channel.SendConfirmAsync("🤔", listArr[rng.Next(0, listArr.Length)]).ConfigureAwait(false);
    }

    [Cmd, Aliases]
    public async Task EightBall([Remainder] string? question = null)
    {
        if (string.IsNullOrWhiteSpace(question))
            return;

        var res = Service.GetEightballResponse(question);
        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithColor(Mewdeko.OkColor)
            .WithDescription(ctx.User.ToString())
            .AddField(efb => efb.WithName($"❓ {GetText("question")}").WithValue(question).WithIsInline(false))
            .AddField($"🎱 {GetText("8ball")}", res));
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task RateGirl(IGuildUser usr)
    {
        var dbUser = await _db.GetOrCreateUser(usr);
        if (dbUser.IsDragon)
        {
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithFooter("credit: r/place")
                .WithDescription(GetText("dragon_goes_nom"))
                .WithImageUrl("https://cdn.discordapp.com/attachments/839193628525330482/962026674122281020/unknown.png");
            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await ReplyErrorLocalizedAsync("dragon_goes_suk");
    }

    private double NextDouble(double x, double y) => (_rng.NextDouble() * (y - x)) + x;

    [Cmd, Aliases]
    public async Task Linux(string guhnoo, string loonix) =>
        await ctx.Channel.SendConfirmAsync(
            $@"I'd just like to interject for moment. What you're refering to as {loonix}, is in fact, {guhnoo}/{loonix}, or as I've recently taken to calling it, {guhnoo} plus {loonix}. {loonix} is not an operating system unto itself, but rather another free component of a fully functioning {guhnoo} system made useful by the {guhnoo} corelibs, shell utilities and vital system components comprising a full OS as defined by POSIX.

Many computer users run a modified version of the {guhnoo} system every day, without realizing it. Through a peculiar turn of events, the version of {guhnoo} which is widely used today is often called {loonix}, and many of its users are not aware that it is basically the {guhnoo} system, developed by the {guhnoo} Project.

There really is a {loonix}, and these people are using it, but it is just a part of the system they use. {loonix} is the kernel: the program in the system that allocates the machine's resources to the other programs that you run. The kernel is an essential part of an operating system, but useless by itself; it can only function in the context of a complete operating system. {loonix} is normally used in combination with the {guhnoo} operating system: the whole system is basically {guhnoo} with {loonix} added, or {guhnoo}/{loonix}. All the so-called {loonix} distributions are really distributions of {guhnoo}/{loonix}."
        ).ConfigureAwait(false);

    [Cmd, Aliases, HelpDisabled]
    public async Task Dragon()
    {
        var user = await _db.GetOrCreateUser(ctx.User);
        user.IsDragon = !user.IsDragon;
        await _db.SaveChangesAsync();
        await ReplyConfirmLocalizedAsync(user.IsDragon ? "dragon_set" : "dragon_unset").ConfigureAwait(false);
    }
}
