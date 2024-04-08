using System.IO;
using Discord.Interactions;
using Mewdeko.Common.Configs;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Administration.Services;
using OtpNet;
using QRCoder;

namespace Mewdeko.Modules.Administration;

public partial class SlashAdministration
{
    /// <summary>
    /// The module for server recovery. Allows you to recover your server access level if th owner account is lost.
    /// </summary>
    [Group("serverrecovery", "Server recovery stuffs")]
    public class SlashServerRecovery : MewdekoSlashModuleBase<ServerRecoveryService>
    {
        private readonly IDataCache cache;
        private readonly IBotCredentials credentials;
        private readonly BotConfig botConfig;

        /// <summary>
        /// Initializes a new instance of the <see cref="SlashServerRecovery"/> class.
        /// </summary>
        /// <param name="cache"></param>
        /// <param name="credentials"></param>
        public SlashServerRecovery(IDataCache cache, IBotCredentials credentials, BotConfig botConfig)
        {
            this.cache = cache;
            this.credentials = credentials;
            this.botConfig = botConfig;
        }

        /// <summary>
        /// Clears data for server recovery.
        /// </summary>
        /// <remarks>
        /// This command clears data related to server recovery.
        /// </remarks>
        [SlashCommand("clear-server-recover", "Clears data for server recovery")]
        public async Task ClearServerRecover()
        {
            var (setup, store) = await Service.RecoveryIsSetup(ctx.Guild.Id);

            if (!setup)
            {
                await ctx.Interaction.SendErrorAsync(GetText("nothingsetup"), botConfig);
            }
            else
            {
                if (await PromptUserConfirmAsync(GetText("areyouabsolutelysure"), ctx.User.Id))
                {
                    await Service.ClearRecoverySetup(store);
                    await ctx.Interaction.SendErrorFollowupAsync(GetText("recoverydeleted"), botConfig);
                }
            }
        }


        /// <summary>
        /// Initiates a server recovery, or starts recovery setup.
        /// </summary>
        /// <remarks>
        /// This command initiates a server recovery or starts the recovery setup process. If the recovery setup has not been done yet, it generates a random secret key and recovery key, sends the recovery key to the user's DM, and displays a QR code for 2FA setup. Otherwise, it prompts the user to enter the recovery key.
        /// </remarks>
        [SlashCommand("server-recover", "Initiates a server recovery, or starts recovery setup.")]
        public async Task ServerRecover()
        {
            await DeferAsync(true);
            var curBotUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
            if (!curBotUser.GuildPermissions.Has(GuildPermission.Administrator))
            {
                await ctx.Interaction.SendErrorFollowupAsync(GetText("recovernoadmin"), botConfig);
                return;
            }

            var (setup, store) = await Service.RecoveryIsSetup(ctx.Guild.Id);
            var db = cache.Redis.GetDatabase();
            if (!setup)
            {
                if (ctx.Guild.OwnerId != ctx.User.Id)
                {
                    await ctx.Interaction.SendErrorFollowupAsync(GetText("recoverowneronly"), botConfig);
                    return;
                }

                var secretKey = KeyGeneration.GenerateRandomKey(); //Generates a random secret key
                var base32Secret = Base32Encoding.ToString(secretKey);

                var otpauth =
                    $"otpauth://totp/{ctx.User.Username}?secret={Base32Encoding.ToString(secretKey)}&issuer={ctx.Client.CurrentUser.Username}";

                var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(otpauth, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(20);
                var secureString = StringExtensions.GenerateSecureString(30);

                await db.StringSetAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}", base32Secret);
                await db.StringSetAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}", secureString);

                var component = new ComponentBuilder().WithButton("Click to enter 2fa", customId: "2fa-verify-setup")
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
                catch
                {
                    await ctx.Interaction.SendErrorFollowupAsync("cant_dm", botConfig);
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


        /// <summary>
        /// Handles the interaction when the user clicks on the "Recovery Key" button.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [ComponentInteraction("recoverykey", true)]
        public Task SendRecoveryKeyModal()
            => RespondWithModalAsync<RecoveryKeyModal>("recoverykeymodal");

        /// <summary>
        /// Handles the interaction when the user submits the recovery key modal.
        /// </summary>
        /// <param name="modal">The recovery key modal.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
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
                    await ctx.Interaction.SendErrorAsync(GetText("tryagain"), botConfig);
                    await db.KeyDeleteAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}");
                    await db.KeyDeleteAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}");
                }
            }
        }

        /// <summary>
        /// Handles the interaction when the user clicks on a 2FA verification button.
        /// </summary>
        /// <param name="type">The type of 2FA verification.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [ComponentInteraction("2fa-verify-*", true)]
        public Task SendTwoFactorModal(string type)
            => RespondWithModalAsync<TwoFactorModal>($"twofactormodal-{type}");


        /// <summary>
        /// Handles the interaction when the user submits the 2FA modal.
        /// </summary>
        /// <param name="type">The type of 2FA verification.</param>
        /// <param name="modal">The 2FA modal.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
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
                        await ctx.Interaction.SendErrorAsync(GetText("incorrect2fa"), botConfig);
                        await db.KeyDeleteAsync($"{credentials.RedisKey()}_rescuekey_{ctx.User.Id}");
                        await db.KeyDeleteAsync($"{credentials.RedisKey()}_2fa_{ctx.User.Id}");
                    }
                }
                else
                {
                    await ctx.Interaction.SendErrorAsync(GetText("startagain"), botConfig);
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
                    await ctx.Interaction.SendErrorAsync(GetText("incorrect2fa"), botConfig);
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
}