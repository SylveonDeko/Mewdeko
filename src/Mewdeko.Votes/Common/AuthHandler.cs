using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mewdeko.Votes.Common;

public class AuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SCHEME_NAME = "AUTHORIZATION_SCHEME";
    public const string TOPGG_CLAIM = "TOPGG_CLAIM";

    private readonly IConfiguration _conf;

    public AuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration conf)
        : base(options, logger, encoder, clock) =>
        _conf = conf;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>();

        if (_conf[ConfKeys.TOPGG_KEY] == Request.Headers["Authorization"].ToString().Trim())
            claims.Add(new Claim(TOPGG_CLAIM, "true"));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims)), SCHEME_NAME)));
    }
}