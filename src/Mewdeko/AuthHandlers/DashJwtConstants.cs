namespace Mewdeko.AuthHandlers;

/// <summary>
///     Shared constants for the dashboard user-identity JWT scheme. Tokens are
///     minted by the SvelteKit dashboard (signed with the shared
///     <c>JwtSecret</c> credential) and verified by every bot instance, giving
///     controllers a trustworthy Discord user identity instead of the anonymous
///     shared-API-key principal.
/// </summary>
public static class DashJwtConstants
{
    /// <summary>
    ///     Authentication scheme name for the dashboard user JWT.
    /// </summary>
    public const string SchemeName = "DashJwt";

    /// <summary>
    ///     Authorization policy requiring a valid dashboard user JWT.
    /// </summary>
    public const string PolicyName = "DashUserPolicy";

    /// <summary>
    ///     Expected token issuer. Must match the value the dashboard mints with.
    /// </summary>
    public const string Issuer = "mewdeko-dashboard";

    /// <summary>
    ///     Expected token audience. Must match the value the dashboard mints with.
    /// </summary>
    public const string Audience = "mewdeko-botapi";

    /// <summary>
    ///     Expected value of the <see cref="ScopeClaim" /> claim for tokens that
    ///     are allowed to reach the bot API.
    /// </summary>
    public const string BackendScope = "botapi";

    /// <summary>
    ///     Claim carrying the Discord user id of the dashboard user.
    /// </summary>
    public const string UserIdClaim = "sub";

    /// <summary>
    ///     Claim carrying the Discord username of the dashboard user.
    /// </summary>
    public const string UserNameClaim = "name";

    /// <summary>
    ///     Claim carrying the unique token id, recorded in the audit log.
    /// </summary>
    public const string TokenIdClaim = "jti";

    /// <summary>
    ///     Claim carrying the token scope. Only <see cref="BackendScope" /> tokens
    ///     are accepted by the bot API.
    /// </summary>
    public const string ScopeClaim = "scope";

    /// <summary>
    ///     Mints a short-lived dashboard-style token so the master can call an owner-only route of a custom bot as
    ///     one of the main bot's owners.
    /// </summary>
    /// <param name="secret">The shared JWT secret.</param>
    /// <param name="userId">The owner the token names.</param>
    /// <param name="lifetime">How long the token stays valid.</param>
    /// <returns>The signed token.</returns>
    public static string MintToken(string secret, ulong userId, TimeSpan lifetime)
    {
        var descriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim(UserIdClaim, userId.ToString()),
                new System.Security.Claims.Claim(UserNameClaim, "master"),
                new System.Security.Claims.Claim(ScopeClaim, BackendScope),
                new System.Security.Claims.Claim(TokenIdClaim, Guid.NewGuid().ToString("N"))
            ]),
            Expires = DateTime.UtcNow.Add(lifetime),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret)),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)
        };
        return new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(descriptor);
    }
}
