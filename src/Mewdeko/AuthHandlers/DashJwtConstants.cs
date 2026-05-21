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
}
