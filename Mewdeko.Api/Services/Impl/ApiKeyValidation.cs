using Mewdeko.Api.Common;

namespace Mewdeko.Api.Services.Impl;

public class ApiKeyValidation(IConfiguration configuration) : IApiKeyValidation
{
    public bool IsValidApiKey(string userApiKey)
    {
        if (string.IsNullOrWhiteSpace(userApiKey))
            return false;

        var apiKey = configuration.GetValue<string>(ApiConstants.KeyName);

        return apiKey != null && apiKey == userApiKey;
    }
}