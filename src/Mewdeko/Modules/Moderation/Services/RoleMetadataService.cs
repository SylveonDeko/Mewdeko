using System.Net.Http;
using System.Text.Json;
using Discord.Rest;
using Mewdeko.Common.ModuleBehaviors;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Moderation.Services;

/// <summary>
/// Service for managing role connection metadata.
/// </summary>
/// <param name="dbContext">The database service</param>
/// <param name="client">The Discord client</param>
/// <param name="botCredentials">The bot credentials</param>
public class RoleMetadataService(MewdekoContext dbContext, DiscordShardedClient client, IBotCredentials botCredentials)
    : INService
{
    private IBotCredentials botCredentials = botCredentials;

    private readonly List<RoleConnectionMetadataProperties> props =
    [
        new(RoleConnectionMetadataType.IntegerGreaterOrEqual, "total_cmds", "Total Commands",
            "The total commands a user has run since we started keeping track"),

        new(RoleConnectionMetadataType.DateTimeGreaterOrEqual, "earliest_use", "First Command",
            "Days since this user's first command after we started keeping track")
    ];

    /// <summary>
    /// Updates the role connection data for a user.
    /// </summary>
    /// <param name="tokenId">The token id</param>
    /// <param name="clientId">The client id</param>
    /// <param name="clientSecret">The client secret</param>
    /// <param name="client">The HTTP client</param>
    /// <param name="dbContext">The unit of work service</param>
    /// <returns></returns>
    public static async Task<string> RefreshUserToken(int tokenId, ulong clientId, string clientSecret,
        HttpClient client, MewdekoContext dbContext)
    {
        var val = await dbContext.AuthCodes.GetById(tokenId);
        var resp = await client.PostAsync("https://discord.com/api/v10/oauth2/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {
                    "client_id", clientId.ToString()
                },
                {
                    "client_secret", clientSecret
                },
                {
                    "grant_type", "refresh_token"
                },
                {
                    "refresh_token", val.RefreshToken
                }
            }));
        var data =
            JsonSerializer.Deserialize<SlashRoleMetadataCommands.AuthResponce>(await resp.Content.ReadAsStringAsync());
        val.ExpiresAt = DateTime.UtcNow + TimeSpan.FromSeconds(data.expires_in);
        val.Token = data.access_token;
        val.RefreshToken = data.refresh_token;
        dbContext.AuthCodes.Update(val);
        await dbContext.SaveChangesAsync();
        return data.access_token;
    }

    /// <summary>
    /// Updates the role connection data for a user.
    /// </summary>
    /// <param name="userId">The user id</param>
    /// <param name="tokenId">The token id</param>
    /// <param name="dbContext">The unit of work service</param>
    /// <param name="clientId">The client id</param>
    /// <param name="clientSecret">The client secret</param>
    /// <param name="client">The HTTP client</param>
    public static async Task UpdateRoleConnectionData(ulong userId, int tokenId, MewdekoContext dbContext, ulong clientId,
        string clientSecret, HttpClient client)
    {
        var tokenData = await dbContext.AuthCodes.GetById(tokenId);
        var cmds = dbContext.CommandStats.Where(x => x.UserId == userId).ToList();
        var count = cmds.Count;
        var date = cmds.OrderByDescending(x => x.NameOrId).First().DateAdded;
        var dbu = dbContext.DiscordUser.FirstOrDefault(y => y.UserId == userId);

        var token = tokenData.Token;
        if (tokenData.ExpiresAt <= DateTime.UtcNow)
            token = await RefreshUserToken(tokenId, clientId, clientSecret, client, dbContext);

        await using var dClient = new DiscordRestClient();

        await dClient.LoginAsync(TokenType.Bearer, token);
        try
        {
            await dClient.ModifyUserApplicationRoleConnectionAsync(clientId, new RoleConnectionProperties("Mewdeko",
                $"User #{(dbu?.Id.ToString() ?? "unknown")}", new Dictionary<string, string>
                {
                    {
                        "total_cmds", count.ToString()
                    },
                    {
                        "earliest_use", date.ToString()
                    }
                }));
        }
        catch (TaskCanceledException)
        {
            /*ignored*/
        }
    }
}