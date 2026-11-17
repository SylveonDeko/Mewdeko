namespace Mewdeko.Services.Impl;

/// <summary>
///     Implementation for pai key validation
/// </summary>
/// <param name="configuration"></param>
public class ApiKeyValidation(BotCredentials configuration) : IApiKeyValidation
{
    /// <inheritdoc />
    public bool IsValidApiKey(string userApiKey)
    {
        if (string.IsNullOrWhiteSpace(userApiKey))
            return false;

        return Matches(configuration.ApiKey, userApiKey);
    }

    /// <summary>
    ///     Compares a configured key with a supplied one in constant time.
    /// </summary>
    /// <param name="expected">The configured key.</param>
    /// <param name="supplied">The key from the request.</param>
    /// <returns>True when both are non-empty and equal.</returns>
    public static bool Matches(string? expected, string? supplied)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
            return false;

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(supplied));
    }
}