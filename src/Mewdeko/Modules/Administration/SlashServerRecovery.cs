using System.IO;
using Discord.Interactions;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Administration.Services;
using OtpNet;

namespace Mewdeko.Modules.Administration;

[Group("administration", "Server administration stuffs")]
public class SlashServerRecovery : MewdekoSlashModuleBase<ServerRecoveryService>
{
    private readonly IDataCache cache;
    private readonly IBotCredentials credentials;

    public SlashServerRecovery(IDataCache cache, IBotCredentials credentials)
    {
        this.cache = cache;
        this.credentials = credentials;
    }

    [SlashCommand("clear-server-recover", "Clears data for server recovery")]
    public async Task ClearServerRecover()
    {
        var (setup, store) = await Service.RecoveryIsSetup(ctx.Guild.Id);

        if (!setup)
        {
            await ctx.Interaction.SendErrorAsync(GetText("nothingsetup"));
        }
        else
        {
            if (await PromptUserConfirmAsync(GetText("areyouabsolutelysure"), ctx.User.Id))
            {
                await Service.ClearRecoverySetup(store);
                await ctx.Interaction.SendErrorFollowupAsync(GetText("recoverydeleted"));
            }
        }
    }

    [SlashCommand("server-recover", "Initiates a server recovery, or starts recovery setup.")]
    public async Task ServerRecover()
    {
        await DeferAsync(true);
        var curBotUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
        if (!curBotUser.GuildPermissions.Has(GuildPermission.Administrator))
        {
            await ctx.Interaction.SendErrorFollowupAsync(GetText("recovernoadmin"));
            return;
        }

        var (setup, store) = await Service.RecoveryIsSetup(ctx.Guild.Id);
        var db = cache.Redis.GetDatabase();
        if (!setup)
        {
            if (ctx.Guild.OwnerId != ctx.User.Id)
            {
                await ctx.Interaction.SendErrorFollowupAsync(GetText("recoverowneronly"));
                return;
            }

            var secretKey = KeyGeneration.GenerateRandomKey(); //Generates a random secret key
            var base32Secret = Base32Encoding.ToString(secretKey);

            var otpauth =
                $"otpauth://totp/{ctx.User.Username}?secret={Base32Encoding.ToString(secretKey)}&issuer={ctx.Client.CurrentUser.Username}";

            var qrGenerator = new QRCoder.QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(otpauth, QRCoder.QRCodeGenerator.ECCLevel.Q);
            var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(20);
            var secureString = StringExtensions.GenerateSecureString(30);

            await db.StringSetAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}", base32Secret);
            await db.StringSetAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}", secureString);

            var component = new ComponentBuilder().WithButton("Click to enter 2fa", customId: $"2fa-verify-setup")
                .WithButton("Download Authy (iOS)", style: ButtonStyle.Link,
                    url: "https://apps.apple.com/us/app/twilio-authy/id494168017")
                .WithButton("Download Authy (Android)", style: ButtonStyle.Link,
                    url: "https://play.google.com/store/apps/details?id=com.authy.authy")
                .WithButton("Download Authy (macOS/Windows)", style: ButtonStyle.Link,
                    url: "https://authy.com/download/");

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(GetText("keepsecret"))
                .WithDescription($"\nRecovery Key: {secureString}" +
                                 $"\n2FA Key ***only***: {base32Secret}" +
                                 $"\n***The recovery key will also be sent to your dms.***");

            await ctx.Interaction.FollowupWithFileAsync(new MemoryStream(qrCodeImage),
                "qrcode.png", embed: eb.WithImageUrl("attachment://qrcode.png").Build(), ephemeral: true,
                components: component.Build());

            try
            {
                var dmEmbed = new EmbedBuilder()
                    .WithTitle($"Recovery Key for {ctx.Guild}")
                    .WithDescription(secureString);
                await ctx.User.SendMessageAsync(embed: dmEmbed.Build());
            }
            catch (Exception e)
            {
                await ctx.Interaction.SendErrorFollowupAsync("cant_dm");
            }
        }
        else
        {
            var components = new ComponentBuilder().WithButton("Enter Recovery Key", "recoverykey");
            await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription("Please follow the steps to recover your server.").Build(),
                components: components.Build());

            await db.StringSetAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}", store.TwoFactorKey);
            await db.StringSetAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}", store.RecoveryKey);
        }
    }

    [ComponentInteraction("recoverykey", true)]
    public Task SendRecoveryKeyModal()
        => RespondWithModalAsync<RecoveryKeyModal>("recoverykeymodal");

    [ModalInteraction("recoverykeymodal", true)]
    public async Task HandleRecoveryKey(RecoveryKeyModal modal)
    {
        var db = cache.Redis.GetDatabase();
        var rescue = await db.StringGetAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}");
        if (rescue.HasValue)
        {
            if (modal.RecoveryKey == rescue)
            {
                var component = new ComponentBuilder().WithButton(GetText("enter2fa"), "2fa-verify-rescue").Build();
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                    .WithDescription(GetText("pleaseenter2fa"))
                    .WithOkColor()
                    .Build(), components: component);
            }

            else
            {
                await ctx.Interaction.SendErrorAsync(GetText("tryagain"));
                await db.KeyDeleteAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}");
                await db.KeyDeleteAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}");
            }
        }
    }

    [ComponentInteraction("2fa-verify-*", true)]
    public Task SendTwoFactorModal(string type)
        => RespondWithModalAsync<TwoFactorModal>($"twofactormodal-{type}");

    [ModalInteraction("twofactormodal-*", true)]
    public async Task HandleTwoFactor(string type, TwoFactorModal modal)
    {
        var db = cache.Redis.GetDatabase();
        if (type is "setup")
        {
            var secretKey = await db.StringGetAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}");
            var rescueKey = await db.StringGetAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}");
            if (secretKey.HasValue)
            {
                var secret = Base32Encoding.ToBytes(secretKey);
                var totp = new Totp(secret);
                var isValid = totp.VerifyTotp(modal.Code, out _, new VerificationWindow(2, 2));


                if (isValid)
                {
                    await Service.SetupRecovery(ctx.Guild.Id, rescueKey, Base32Encoding.ToString(secret));
                    await ctx.Interaction.SendConfirmAsync(GetText("recoverysetupcomplete"));
                    await db.KeyDeleteAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}");
                    await db.KeyDeleteAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}");
                }
                else
                {
                    await ctx.Interaction.SendErrorAsync(GetText("incorrect2fa"));
                    await db.KeyDeleteAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}");
                    await db.KeyDeleteAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}");
                }
            }
            else
            {
                await ctx.Interaction.SendErrorAsync(GetText("startagain"));
            }
        }
        else
        {
            var (_, store) = await Service.RecoveryIsSetup(ctx.Guild.Id);

            var key = Base32Encoding.ToBytes(store.TwoFactorKey);
            var totp = new Totp(key);
            var isValid = totp.VerifyTotp(modal.Code, out _, new VerificationWindow(2, 2));
            if (!isValid)
            {
                await ctx.Interaction.SendErrorAsync(GetText("incorrect2fa"));
                await db.KeyDeleteAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}");
                await db.KeyDeleteAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}");
                return;
            }

            await db.KeyDeleteAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}");
            await db.KeyDeleteAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}");
            var currentBotUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
            var highestRole = currentBotUser.GetRoles().Max(role => role.Position);
            var newRole = await ctx.Guild.CreateRoleAsync("Recovered Server Owner Role",
                new GuildPermissions(administrator: true), null, false, null);
            await newRole.ModifyAsync(x => x.Position = highestRole - 1);
            var curuser = ctx.User as IGuildUser;
            await curuser.AddRoleAsync(newRole);
            await ctx.Interaction.SendConfirmAsync(GetText("highestpossiblerole"));
        }
    }
}