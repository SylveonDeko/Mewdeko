namespace Mewdeko.Api.Services;

public interface IApiKeyValidation
{
    bool IsValidApiKey(string userApiKey);
}