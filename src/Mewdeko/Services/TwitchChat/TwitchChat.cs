using System.Net.Http;
using System.Text.Json;
using Serilog;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Api;

namespace Mewdeko.Services.TwitchChat
{
    public class TwitchChat
    {
        TwitchClient twitchClient = new TwitchClient();
        private Dictionary<string, DateTime> userRateLimit = new Dictionary<string, DateTime>();
        public class TokenResponse
        {
            public string access_token { get; set; }
            public int expires_in { get; set; }
            public string token_type { get; set; }
        }

        private void TwitchClient_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            // Rate Limit mitigation
            if (userRateLimit.ContainsKey(e.ChatMessage.Username))
            {
                if (DateTime.Now - userRateLimit[e.ChatMessage.Username] < TimeSpan.FromSeconds(5))
                {
                    return; // ignore messages for user from duration of timeout
                }
            }

            Task.Run(() =>
            {
                // Process message
                var user = e.ChatMessage.Username;
                var message = e.ChatMessage.Message;

                Log.Information($"{user}:{message}");
            });
        }
        private void TwitchClient_OnLog(object sender, OnLogArgs e)
        {
            Log.Information($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private void TwitchClient_OnConnected(object sender, OnConnectedArgs e)
        {
            Log.Information($"Connected to {e.AutoJoinChannel}");
        }

        private void TwitchClient_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {

            Task.Run(() =>
            {
                // Process cmd
                var command = e.Command.CommandText;
            });
            
        }

        private void TwitchClient_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Log.Information($"Joined channel: {e.Channel}");
            twitchClient.SendMessage(e.Channel, "Hello World!");
        }

        public async Task InitializeClient()
        {
            await Task.Run(async () =>
            {
                try
                {
                    await Task.Run(async () =>
                    {
                        var username = Environment.GetEnvironmentVariable("TWITCH_USERNAME");
                        var clientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID");
                        var secret = Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET");
                        var redirectUri = "httpL//localhost";

                        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret))
                        {
                            Log.Information("Twitch Client not initialized. Missing environment variables.");
                            return;
                        }

                        var token = await GetOauthTokenAsync(clientId, secret, redirectUri);

                        if (string.IsNullOrEmpty(token))
                        {
                            Log.Information("OAUTH Token is null or empty");
                            return;
                        }

                        // Generate the API object
                        var api = new TwitchAPI();
                        api.Settings.ClientId = clientId;
                        api.Settings.AccessToken = token;
                        var newtoken = await api.Auth.RefreshAuthTokenAsync(token, secret, clientId);
                        twitchClient.Initialize(new ConnectionCredentials("DaxxTrias", newtoken.AccessToken));


                        // Generate the Client object
                        twitchClient.OnMessageReceived += TwitchClient_OnMessageReceived;
                        twitchClient.OnChatCommandReceived += TwitchClient_OnChatCommandReceived;
                        twitchClient.OnJoinedChannel += TwitchClient_OnJoinedChannel;
                        twitchClient.OnLog += TwitchClient_OnLog;
                        twitchClient.OnConnected += TwitchClient_OnConnected;

                        twitchClient.Connect();
                        twitchClient.JoinChannel("DaxxTrias");
                    });

                    if (twitchClient.IsConnected)
                        Log.Information("Twitch Client Initialized");
                }
                catch (Exception ex)
                {
                    Log.Information($"Error: {ex.Message}\nStack Trace: {ex.StackTrace}");
                }
            });
        }

        private async Task<string> GetOauthTokenAsync(string clientId, string clientSecret, string redirectUri)
        {
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                Log.Information("ClientID, ClientSecret, or Auth Code is null or empty");
                return null;
            }

            using (var httpClient = new HttpClient())
            {
                var postData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri),
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("scope", "chat:read chat:edit"),
                });

                var response = await httpClient.PostAsync("https://id.twitch.tv/oauth2/token", postData);
                var responseString = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(responseString))
                {
                    Log.Information("Response string is null or empty.");
                    return null;
                }

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseString);

                        if (tokenResponse != null)
                            return tokenResponse?.access_token;
                    }
                    catch (Exception ex)
                    {
                        Log.Information($"Deserialization error: {ex.Message}");
                        return null;
                    }
                }
                if (!response.IsSuccessStatusCode)
                {
                    Log.Information($"Failed to get OAuth token. HTTP status: {response.StatusCode}\n response: {responseString}");
                    return null;
                }
                else
                {
                    Log.Information($"Error occured during OAuthToken generation. HTTP request: {response.StatusCode}");
                    return null;
                }
            }
        }
    }
}
