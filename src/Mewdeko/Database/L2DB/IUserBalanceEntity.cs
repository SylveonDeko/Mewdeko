namespace DataModel;

/// <summary>
///     Shared shape of the guild-scoped and global currency balance rows, so the atomic balance
///     operations can be written once against either table instead of duplicated per scope.
/// </summary>
public interface IUserBalanceEntity
{
    /// <summary>
    ///     The user the balance belongs to.
    /// </summary>
    ulong UserId { get; set; }

    /// <summary>
    ///     Spendable currency. This is what games, the shop and robbery draw from.
    /// </summary>
    long Balance { get; set; }

    /// <summary>
    ///     Currency held in the bank. Safe from robbery and can earn interest.
    /// </summary>
    long Bank { get; set; }
}