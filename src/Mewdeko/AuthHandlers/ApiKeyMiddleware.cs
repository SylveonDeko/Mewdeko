using System.Security.Claims;
using System.Text.Encodings.Web;
using Mewdeko.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mewdeko.AuthHandlers;

/// <inheritdoc />
public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiKeyValidation apiKeyValidation;

    /// <inheritdoc />
    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyValidation apiKeyValidation)
        : base(options, logger, encoder)
    {
        this.apiKeyValidation = apiKeyValidation;
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiConstants.HeaderName, out var apiKeyHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("API Key is missing"));
        }

        var providedApiKey = apiKeyHeaderValues.ToString();

        if (!apiKeyValidation.IsValidApiKey(providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ApiUser")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}