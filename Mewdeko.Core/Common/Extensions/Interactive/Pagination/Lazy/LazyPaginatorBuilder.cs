using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace Mewdeko.Interactive.Pagination
{
    /// <summary>
    /// Represents a builder class for making a <see cref="LazyPaginator"/>.
    /// </summary>
    public sealed class LazyPaginatorBuilder : PaginatorBuilder<LazyPaginator, LazyPaginatorBuilder>
    {
        /// <summary>
        /// Gets or sets the method used to load the pages of the paginator lazily.
        /// </summary>
        public Func<int, Task<PageBuilder>> PageFactory { get; set; }

        /// <summary>
        /// Gets or sets the maximum page index of the paginator.
        /// </summary>
        public int MaxPageIndex { get; set; }

        /// <summary>
        /// Gets or sets whether to cache loaded pages. The default value is <see langword="true"/>.
        /// </summary>
        public bool CacheLoadedPages { get; set; } = true;

        public override LazyPaginator Build(int startPageIndex = 0)
        {
            if (Options.Count == 0)
            {
                WithDefaultEmotes();
            }

            return new LazyPaginator(
                Users?.ToArray() ?? Array.Empty<IUser>(),
                new ReadOnlyDictionary<IEmote, PaginatorAction>(Options), // TODO: Find a way to create an ImmutableDictionary without getting the contents reordered.
                CanceledPage?.Build(),
                TimeoutPage?.Build(),
                Deletion,
                InputType,
                ActionOnCancellation,
                ActionOnTimeout,
                AddPaginatorFooterAsync,
                startPageIndex,
                MaxPageIndex,
                CacheLoadedPages);

            async Task<Page> AddPaginatorFooterAsync(int page)
            {
                var builder = await PageFactory(page).ConfigureAwait(false);

                return builder?.WithPaginatorFooter(Footer, page, MaxPageIndex, Users)
                               .Build();
            }
        }

        /// <summary>
        /// Sets the <see cref="PageFactory"/> of the paginator.
        /// </summary>
        public LazyPaginatorBuilder WithPageFactory(Func<int, Task<PageBuilder>> pageFactory)
        {
            PageFactory = pageFactory;
            return this;
        }

        /// <summary>
        /// Sets the maximum page index of the paginator.
        /// </summary>
        public LazyPaginatorBuilder WithMaxPageIndex(int maxPageIndex)
        {
            MaxPageIndex = maxPageIndex;
            return this;
        }

        /// <summary>
        /// Sets whether to cache loaded pages.
        /// </summary>
        public LazyPaginatorBuilder WithCacheLoadedPages(bool cacheLoadedPages)
        {
            CacheLoadedPages = cacheLoadedPages;
            return this;
        }
    }
}