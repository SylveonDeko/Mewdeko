using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;

namespace Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;

/// <summary>
///     Represents a lazy paginator.
/// </summary>
public sealed class LazyPaginator : Paginator
{
    private readonly Dictionary<int, Page> _cachedPages;

    internal LazyPaginator(IReadOnlyCollection<IUser> users, IReadOnlyDictionary<IEmote, PaginatorAction> emotes,
        Page canceledPage, Page timeoutPage, DeletionOptions deletion, InputType inputType,
        ActionOnStop actionOnCancellation, ActionOnStop actionOnTimeout, Func<int, Task<Page>> pageFactory,
        int startPage, int maxPageIndex, bool cacheLoadedPages)
        : base(users, emotes, canceledPage, timeoutPage, deletion, inputType, actionOnCancellation, actionOnTimeout,
            startPage)
    {
        PageFactory = pageFactory;
        MaxPageIndex = maxPageIndex;
        CacheLoadedPages = cacheLoadedPages;

        if (CacheLoadedPages) _cachedPages = new Dictionary<int, Page>();
    }

    /// <summary>
    ///     Gets the function used to load the pages of this paginator lazily.
    /// </summary>
    public Func<int, Task<Page>> PageFactory { get; }

    /// <inheritdoc />
    public override int MaxPageIndex { get; }

    /// <summary>
    ///     Gets whether to cache loaded pages.
    /// </summary>
    public bool CacheLoadedPages { get; }

    /// <inheritdoc />
    public override async Task<Page> GetOrLoadPageAsync(int pageIndex)
    {
        if (CacheLoadedPages && _cachedPages != null && _cachedPages.TryGetValue(pageIndex, out var page))
            return page;

        page = await PageFactory(pageIndex).ConfigureAwait(false);
        _cachedPages?.TryAdd(pageIndex, page);

        return page;
    }
}