using System.Net.Http;
using System.Text.Json;
using Discord.Interactions;
using Discord.Rest;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Moderation;

/// <summary>
/// Module for managing role metadata.
/// </summary>
public class SlashRoleMetadataCommands : MewdekoSlashSubmodule
{
    private static readonly HttpClient HttpClient = new();

    /// <summary>
    /// The bot credentials.
    /// </summary>
    public IBotCredentials Credentials { get; set; }

    /// <summary>
    /// The config service for yml bot config.
    /// </summary>
    public BotConfigService ConfigService { get; set; }

    /// <summary>
    /// The database service.
    /// </summary>
    public MewdekoContext dbContext { get; set; }

    /// <summary>
    /// Component interaction for entering an auth code.
    /// </summary>
    /// <returns></returns>
    [ComponentInteraction("auth_code.enter", true), RequireDragon]
    public Task HandleAuthStepTwo()
        => RespondWithModalAsync<AuthHandshakeStepTwoModal>("auth_code.handshake");

    /// <summary>
    /// Modal for entering an auth code.
    /// </summary>
    /// <param name="modal"></param>
    [ModalInteraction("auth_code.handshake", true)]
    public async Task HandleAuthHandshake(AuthHandshakeStepTwoModal modal)
    {
        await DeferAsync(true);
        // get bearer token
        var code = modal.Code;
        const string url = "https://discord.com/api/v10/oauth2/token";
        HttpContent content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            {
                "client_id", Context.Client.CurrentUser.Id.ToString()
            },
            {
                "client_secret", Credentials.ClientSecret
            },
            {
                "grant_type", "authorization_code"
            },
            {
                "code", code
            },
            {
                "redirect_uri", ConfigService.Data.RedirectUrl
            }
        });
        var req = await HttpClient.PostAsync(url, content);
        var response = JsonSerializer.Deserialize<AuthResponce>(await req.Content.ReadAsStringAsync());

        if (response.access_token.IsNullOrWhiteSpace())
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                "This auth code is probably invalid or expired. Please retry with a different code. If this problem persists, please contact the bot owner.",
                Config);
            return;
        }

        // verify token source
        var client = new DiscordRestClient();
        await client.LoginAsync(TokenType.Bearer, response.access_token);
        if (client.CurrentUser.Id != Context.Interaction.User.Id)
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                "This auth token was not issued to you. Attempting to impersonate another user may result in a permanent ban.",
                Config);
            return;
        }

        var mod = new RoleConnectionAuthStorage
        {
            Scopes = response.scope,
            Token = response.access_token,
            RefreshToken = response.refresh_token,
            ExpiresAt = DateTime.UtcNow.Add(TimeSpan.FromSeconds(response.expires_in)),
            UserId = Context.User.Id
        };


        await dbContext.AuthCodes.AddAsync(mod);
        await dbContext.SaveChangesAsync();

        await Task.Delay(1000);
        await RoleMetadataService.UpdateRoleConnectionData(Context.User.Id, mod.Id, dbContext, Context.Client.CurrentUser.Id,
            Credentials.ClientSecret, HttpClient);

        var eb = new EmbedBuilder()
            .WithTitle("Code Saved")
            .WithDescription(
                "Your authorization code and a valid refresh token have been saved. When mewdeko adds support for role connection metadata you will automatically be enrolled. If you ever want to disable this open your discord settings and go to **Authorized Apps**, and remove this application.")
            .WithColor(Color.Green)
            .AddField("Dragon Notice",
                "If you disable dragon mode on your account before this feature is complete your early-use of it, and related records may be removed.")
            .WithImageUrl(
                "https://media.discordapp.net/attachments/915770282579484693/1064141713385467935/Deactivate_Mewdeko.png?width=618&height=671");
        await FollowupAsync(embed: eb.Build());
    }

    /// <summary>
    /// The response discord gives us when we authorize.
    /// </summary>
    public class AuthResponce
    {
        /// <summary>
        /// The access token.
        /// </summary>
        public string access_token { get; set; }

        /// <summary>
        /// The token type.
        /// </summary>
        public string token_type { get; set; }

        /// <summary>
        /// When the access token expires.
        /// </summary>
        public int expires_in { get; set; }

        /// <summary>
        /// The refresh token.
        /// </summary>
        public string refresh_token { get; set; }

        /// <summary>
        /// The scope the access token has.
        /// </summary>
        public string scope { get; set; }
    }
}