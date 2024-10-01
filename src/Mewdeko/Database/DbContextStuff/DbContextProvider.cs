using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.DbContextStuff;

/// <summary>
///     Provides a service for managing and retrieving instances of MewdekoContext.
///     This class implements INService to integrate with the application's service architecture.
/// </summary>
/// <remarks>
///     This provider uses a factory pattern to create new instances of MewdekoContext,
///     allowing for better control over the lifecycle of database contexts.
/// </remarks>
public class DbContextProvider : INService
{
    private readonly IDbContextFactory<MewdekoContext> contextFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DbContextProvider" /> class.
    /// </summary>
    /// <param name="contextFactory">The factory used to create new instances of MewdekoContext.</param>
    /// <remarks>
    ///     This constructor uses primary constructor syntax, automatically assigning the parameter to a private readonly
    ///     field.
    /// </remarks>
    public DbContextProvider(IDbContextFactory<MewdekoContext> contextFactory)
    {
        this.contextFactory = contextFactory;
    }

    /// <summary>
    ///     Asynchronously retrieves a new instance of MewdekoContext.
    /// </summary>
    /// <returns>
    ///     A <see cref="Task{TResult}" /> representing the asynchronous operation.
    ///     The task result contains a new instance of <see cref="MewdekoContext" />.
    /// </returns>
    /// <remarks>
    ///     This method creates a new context instance each time it's called.
    ///     Ensure proper disposal of the context after use to prevent resource leaks.
    /// </remarks>
    public async Task<MewdekoContext> GetContextAsync()
    {
        return await contextFactory.CreateDbContextAsync();
    }
}