using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mewdeko.AuthHandlers;

/// <inheritdoc />
public class AuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>
    ///
    /// </summary>
    public const string SchemeName = "AUTHORIZATION_SCHEME";
    /// <summary>
    ///
    /// </summary>
    public const string TopggClaim = "TOPGG_CLAIM";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
            { new Claim(TopggClaim, "true") };

        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims)),
                SchemeName)));
    }
}