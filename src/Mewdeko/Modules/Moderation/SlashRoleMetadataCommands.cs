using System.Net.Http;
using System.Text.Json;
using Discord.Interactions;
using Discord.Rest;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Moderation;

public class SlashRoleMetadataCommands : MewdekoSlashSubmodule
{
    private static readonly HttpClient HttpClient = new();
    public IBotCredentials Credentials { get; set; }
    public BotConfigService ConfigService { get; set; }
    public DbService DbService { get; set; }

    [ComponentInteraction("auth_code.enter", true), RequireDragon]
    public async Task HandleAuthStepTwo()
        => await RespondWithModalAsync<AuthHandshakeStepTwoModal>("auth_code.handshake");

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
                "This auth code is probably invalid or expired. Please retry with a different code. If this problem persists, please contact the bot owner.");
            return;
        }

        // verify token source
        var client = new DiscordRestClient();
        await client.LoginAsync(TokenType.Bearer, response.access_token);
        if (client.CurrentUser.Id != Context.Interaction.User.Id)
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                "This auth token was not issued to you. Attempting to impersonate another user may result in a permanent ban.");
            return;
        }

        var mod = new RoleConnectionAuthStorage()
        {
            Scopes = response.scope,
            Token = response.access_token,
            RefreshToken = response.refresh_token,
            ExpiresAt = DateTime.UtcNow.Add(TimeSpan.FromSeconds(response.expires_in)),
            UserId = Context.User.Id
        };

        await using var uow = DbService.GetDbContext();
        await uow.AuthCodes.AddAsync(mod);
        await uow.SaveChangesAsync();

        await Task.Delay(1000);
        await RoleMetadataService.UpdateRoleConnectionData(Context.User.Id, mod.Id, uow, Context.Client.CurrentUser.Id,
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

    public class AuthResponce
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
        public string scope { get; set; }
    }
}