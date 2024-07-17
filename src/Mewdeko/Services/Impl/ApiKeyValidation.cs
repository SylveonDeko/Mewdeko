using Mewdeko.Api.Services;

namespace Mewdeko.Services.Impl;

/// <summary>
/// Implementation for pai key validation
/// </summary>
/// <param name="configuration"></param>
public class ApiKeyValidation(BotCredentials configuration) : IApiKeyValidation
{
    /// <inheritdoc />
    public bool IsValidApiKey(string userApiKey)
    {
        if (string.IsNullOrWhiteSpace(userApiKey))
            return false;

        var apiKey = configuration.ApiKey;

        return apiKey != null && apiKey == userApiKey;
    }
}