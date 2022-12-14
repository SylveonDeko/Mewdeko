using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable UnassignedGetOnlyAutoProperty

namespace Mewdeko.Votes.Services;

public class BotCredentials : IBotCredentials
{
    private string credsFileName = Path.Combine(Directory.GetCurrentDirectory(), "../Mewdeko/credentials.json");


    public BotCredentials()
    {
        try
        {
            File.WriteAllText("./credentials_example.json",
                JsonConvert.SerializeObject(new CredentialsModel(), Formatting.Indented));
        }
        catch
        {
            // ignored
        }

        if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Mewdeko.exe")))
            credsFileName = "credentials.json";
        if (!File.Exists(credsFileName))
        {
            Log.Information(Directory.GetCurrentDirectory());
            Log.Warning(
                $"credentials.json is missing. Attempting to load creds from environment variables prefixed with 'Mewdeko_'. Example is in {Path.GetFullPath("./credentials_example.json")}");
            Environment.Exit(1);
        }

        UpdateCredentials();
    }

    public void UpdateCredentials()
    {
        try
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddJsonFile(credsFileName, true)
                .AddEnvironmentVariables("Mewdeko_");

            var data = configBuilder.Build();

            Token = data[nameof(Token)];
            if (string.IsNullOrWhiteSpace(Token))
            {
                Log.Error(
                    "Token is missing from credentials.json or Environment variables. Add it and restart the program.");
                Environment.Exit(5);
            }

            RedisOptions = !string.IsNullOrWhiteSpace(data[nameof(RedisOptions)]) ? data[nameof(RedisOptions)] : "127.0.0.1,syncTimeout=3000";
        }
        catch (Exception ex)
        {
            Log.Error("JSON serialization has failed. Fix your credentials file and restart the bot");
            Log.Fatal(ex.ToString());
            Environment.Exit(6);
        }
    }

    public string Token { get; set; }

    public string RedisOptions { get; set; }

    /// <summary>
    ///     No idea why this thing exists
    /// </summary>
    private class CredentialsModel : IBotCredentials
    {
        public string Token { get; } = "";
        public string RedisOptions { get; set; }
    }
}