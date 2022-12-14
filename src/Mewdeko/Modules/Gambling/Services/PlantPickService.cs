using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mewdeko.Common.Collections;
using Mewdeko.Services.Impl;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using Image = SixLabors.ImageSharp.Image;

namespace Mewdeko.Modules.Gambling.Services;

public class PlantPickService : INService
{
    private readonly DiscordSocketClient client;
    private readonly ICurrencyService cs;
    private readonly DbService db;
    private readonly FontProvider fonts;
    private readonly GuildSettingsService guildSettings;

    public readonly ConcurrentHashSet<ulong> GenerationChannels;
    private readonly GamblingConfigService gss;
    private readonly IImageCache images;
    private readonly MewdekoRandom rng;
    private readonly IBotStrings strings;
    private readonly SemaphoreSlim pickLock = new(1, 1);

    public PlantPickService(DbService db, CommandHandler cmd, IBotStrings strings,
        IDataCache cache, FontProvider fonts, ICurrencyService cs, DiscordSocketClient client, GamblingConfigService gss,
        GuildSettingsService guildSettings)
    {
        this.db = db;
        this.strings = strings;
        images = cache.LocalImages;
        this.fonts = fonts;
        this.cs = cs;
        rng = new MewdekoRandom();
        this.client = client;
        this.gss = gss;
        this.guildSettings = guildSettings;

        cmd.OnMessageNoTrigger += PotentialFlowerGeneration;
        using var uow = db.GetDbContext();
        var guildIds = client.Guilds.Select(x => x.Id).ToList();
        var configs = uow.GuildConfigs
            .AsQueryable()
            .Include(x => x.GenerateCurrencyChannelIds)
            .Where(x => guildIds.Contains(x.GuildId))
            .ToList();

        GenerationChannels = new ConcurrentHashSet<ulong>(configs
            .SelectMany(c => c.GenerateCurrencyChannelIds.Select(obj => obj.ChannelId)));
    }

    //channelId/last generation
    public ConcurrentDictionary<ulong, DateTime> LastGenerations { get; } = new();

    private string? GetText(ulong gid, string? key, params object?[] rep) => strings.GetText(key, gid, rep);

    public async Task<bool> ToggleCurrencyGeneration(ulong gid, ulong cid)
    {
        bool enabled;
        await using var uow = db.GetDbContext();
        var guildConfig = await uow.ForGuildId(gid, set => set.Include(gc => gc.GenerateCurrencyChannelIds));

        var toAdd = new GcChannelId
        {
            ChannelId = cid
        };
        if (!guildConfig.GenerateCurrencyChannelIds.Contains(toAdd))
        {
            guildConfig.GenerateCurrencyChannelIds.Add(toAdd);
            GenerationChannels.Add(cid);
            enabled = true;
        }
        else
        {
            var toDelete = guildConfig.GenerateCurrencyChannelIds.FirstOrDefault(x => x.Equals(toAdd));
            if (toDelete != null) uow.Remove(toDelete);
            GenerationChannels.TryRemove(cid);
            enabled = false;
        }

        await uow.SaveChangesAsync();

        return enabled;
    }

    public IEnumerable<GuildConfigExtensions.GeneratingChannel> GetAllGeneratingChannels()
    {
        using var uow = db.GetDbContext();
        return (IEnumerable<GuildConfigExtensions.GeneratingChannel>)uow.GuildConfigs.GetGeneratingChannels();
    }

    /// <summary>
    ///     Get a random currency image stream, with an optional password sticked onto it.
    /// </summary>
    /// <param name="pass">Optional password to add to top left corner.</param>
    /// <param name="extension"></param>
    /// <returns>Stream of the currency image</returns>
    public Stream GetRandomCurrencyImage(string pass, out string extension)
    {
        // get a random currency image bytes
        var mewdekoRandom = new MewdekoRandom();
        byte[] curImg;
        try
        {
            curImg = images.Currency[mewdekoRandom.Next(0, images.Currency.Count)];
        }
        catch
        {
            extension = null;
            return Stream.Null;
        }


        if (string.IsNullOrWhiteSpace(pass))
        {
            // determine the extension
            using (Image.Load(curImg, out var format))
            {
                extension = format.FileExtensions.FirstOrDefault() ?? "png";
            }

            // return the image
            return curImg.ToStream();
        }

        // get the image stream and extension
        var (s, ext) = AddPassword(curImg, pass);
        // set the out extension parameter to the extension we've got
        extension = ext;
        // return the image
        return s;
    }

    /// <summary>
    ///     Add a password to the image.
    /// </summary>
    /// <param name="curImg">Image to add password to.</param>
    /// <param name="pass">Password to add to top left corner.</param>
    /// <returns>Image with the password in the top left corner.</returns>
    private (Stream, string) AddPassword(byte[] curImg, string pass)
    {
        // draw lower, it looks better
        pass = pass.TrimTo(10, true).ToLowerInvariant();
        using var img = Image.Load<Rgba32>(curImg, out var format);
        // choose font size based on the image height, so that it's visible
        var font = fonts.NotoSans.CreateFont(img.Height / 12, FontStyle.Bold);
        img.Mutate(x =>
        {
            // measure the size of the text to be drawing
            var size = TextMeasurer.Measure(pass, new TextOptions(font));

            // fill the background with black, add 5 pixels on each side to make it look better
            x.FillPolygon(Color.ParseHex("00000080"),
                new PointF(0, 0),
                new PointF(size.Width + 5, 0),
                new PointF(size.Width + 5, size.Height + 10),
                new PointF(0, size.Height + 10));

            // draw the password over the background
            x.DrawText(pass,
                font,
                Color.White,
                new PointF(0, 0));
        });
        // return image as a stream for easy sending
        return (img.ToStream(format), format.FileExtensions.FirstOrDefault() ?? "png");
    }

    private Task PotentialFlowerGeneration(IUserMessage imsg)
    {
        if (imsg is not SocketUserMessage msg || msg.Author.IsBot)
            return Task.CompletedTask;

        if (imsg.Channel is not ITextChannel channel)
            return Task.CompletedTask;

        if (!GenerationChannels.Contains(channel.Id))
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                var config = gss.Data;
                var lastGeneration = LastGenerations.GetOrAdd(channel.Id, DateTime.MinValue);
                var mewdekoRandom = new MewdekoRandom();

                if (DateTime.UtcNow - TimeSpan.FromSeconds(config.Generation.GenCooldown) <
                    lastGeneration) //recently generated in this channel, don't generate again
                {
                    return;
                }

                var num = mewdekoRandom.Next(1, 101) + (config.Generation.Chance * 100);
                if (num > 100 && LastGenerations.TryUpdate(channel.Id, DateTime.UtcNow, lastGeneration))
                {
                    var dropAmount = config.Generation.MinAmount;
                    var dropAmountMax = config.Generation.MaxAmount;

                    if (dropAmountMax > dropAmount)
                        dropAmount = new MewdekoRandom().Next(dropAmount, dropAmountMax + 1);

                    if (dropAmount > 0)
                    {
                        var prefix = await guildSettings.GetPrefix(channel.Guild.Id);
                        var toSend = dropAmount == 1
                            ? $"{GetText(channel.GuildId, "curgen_sn", config.Currency.Sign)} {GetText(channel.GuildId, "pick_sn", prefix)}"
                            : $"{GetText(channel.GuildId, "curgen_pl", dropAmount, config.Currency.Sign)} {GetText(channel.GuildId, "pick_pl", prefix)}";

                        var pw = config.Generation.HasPassword
                            ? GenerateCurrencyPassword().ToUpperInvariant()
                            : null;

                        IUserMessage sent;
                        await using var stream = GetRandomCurrencyImage(pw, out var ext);
                        if (stream.Length is 0)
                        {
                            sent = await channel.SendMessageAsync(toSend);
                        }
                        else
                        {
                            sent = await channel.SendFileAsync(stream, $"currency_image.{ext}", toSend)
                                .ConfigureAwait(false);
                        }

                        await AddPlantToDatabase(channel.GuildId,
                            channel.Id,
                            client.CurrentUser.Id,
                            sent.Id,
                            dropAmount,
                            pw).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                // ignored
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Generate a hexadecimal string from 1000 to ffff.
    /// </summary>
    /// <returns>A hexadecimal string from 1000 to ffff</returns>
    private string GenerateCurrencyPassword()
    {
        // generate a number from 1000 to ffff
        var num = rng.Next(4096, 65536);
        // convert it to hexadecimal
        return num.ToString("x4");
    }

    public async Task<long> PickAsync(ulong gid, ITextChannel ch, ulong uid, string? pass)
    {
        await pickLock.WaitAsync();
        try
        {
            long amount;
            ulong[] ids;
            await using (var uow = db.GetDbContext())
            {
                // this method will sum all plants with that password,
                // remove them, and get messageids of the removed plants

                pass = pass?.Trim().TrimTo(10, hideDots: true).ToUpperInvariant();
                // gets all plants in this channel with the same password
                var entries = uow.PlantedCurrency
                    .AsQueryable()
                    .Where(x => x.ChannelId == ch.Id && pass == x.Password)
                    .ToList();
                // sum how much currency that is, and get all of the message ids (so that i can delete them)
                amount = entries.Sum(x => x.Amount);
                ids = entries.Select(x => x.MessageId).ToArray();
                // remove them from the database
                uow.RemoveRange(entries);

                if (amount > 0)
                {
                    // give the picked currency to the user
                    await cs.AddAsync(uid, "Picked currency", amount, gamble: false);
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
            }

            try
            {
                // delete all of the plant messages which have just been picked
                var _ = ch.DeleteMessagesAsync(ids);
            }
            catch
            {
                // ignored
            }

            // return the amount of currency the user picked
            return amount;
        }
        finally
        {
            pickLock.Release();
        }
    }

    public async Task<ulong?> SendPlantMessageAsync(ulong gid, IMessageChannel ch, string user, long amount,
        string pass)
    {
        try
        {
            // get the text
            var prefix = await guildSettings.GetPrefix(gid);
            var msgToSend = GetText(gid,
                "planted",
                Format.Bold(user),
                amount + gss.Data.Currency.Sign,
                prefix);

            if (amount > 1)
                msgToSend += $" {GetText(gid, "pick_pl", prefix)}";
            else
                msgToSend += $" {GetText(gid, "pick_sn", prefix)}";

            //get the image
            await using var stream = GetRandomCurrencyImage(pass, out var ext);
            // send it
            IUserMessage msg;
            if (stream.Length is 0)
            {
                msg = await ch.SendMessageAsync(msgToSend);
            }
            else
            {
                msg = await ch.SendFileAsync(stream, $"img.{ext}", msgToSend).ConfigureAwait(false);
            }

            // return sent message's id (in order to be able to delete it when it's picked)
            return msg.Id;
        }
        catch
        {
            // if sending fails, return null as message id
            return null;
        }
    }

    public async Task<bool> PlantAsync(ulong gid, IMessageChannel ch, ulong uid, string user, long amount,
        string? pass)
    {
        // normalize it - no more than 10 chars, uppercase
        pass = pass?.Trim().TrimTo(10, true).ToUpperInvariant();
        // has to be either null or alphanumeric
        if (!string.IsNullOrWhiteSpace(pass) && !pass.IsAlphaNumeric())
            return false;

        // remove currency from the user who's planting
        if (await cs.RemoveAsync(uid, "Planted currency", amount))
        {
            // try to send the message with the currency image
            var msgId = await SendPlantMessageAsync(gid, ch, user, amount, pass).ConfigureAwait(false);
            if (msgId == null)
            {
                // if it fails it will return null, if it returns null, refund
                await cs.AddAsync(uid, "Planted currency refund", amount);
                return false;
            }

            // if it doesn't fail, put the plant in the database for other people to pick
            await AddPlantToDatabase(gid, ch.Id, uid, msgId.Value, amount, pass).ConfigureAwait(false);
            return true;
        }

        // if user doesn't have enough currency, fail
        return false;
    }

    private async Task AddPlantToDatabase(ulong gid, ulong cid, ulong uid, ulong mid, long amount, string pass)
    {
        await using var uow = db.GetDbContext();
        uow.PlantedCurrency.Add(new PlantedCurrency
        {
            Amount = amount,
            GuildId = gid,
            ChannelId = cid,
            Password = pass,
            UserId = uid,
            MessageId = mid
        });
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }
}