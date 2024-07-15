namespace Mewdeko.Api.Services;

/// <summary>
/// Api key validation interface
/// </summary>
public interface IApiKeyValidation
{
    /// <summary>
    /// Checks if the given key is valid
    /// </summary>
    /// <param name="userApiKey">The key to check</param>
    /// <returns>True/False depending on if its correct</returns>
    bool IsValidApiKey(string userApiKey);
}