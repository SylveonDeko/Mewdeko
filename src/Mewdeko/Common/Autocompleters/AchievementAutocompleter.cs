using Discord.Interactions;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Achievements.Services;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Suggests a server's achievements by name, answering with their keys.
/// </summary>
public class AchievementAutocompleter : AutocompleteHandler
{
    private const int MaxSuggestions = 25;
    private const int MaxNameLength = 100;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementAutocompleter" /> class.
    /// </summary>
    /// <param name="service">The achievement service.</param>
    public AchievementAutocompleter(AchievementService service)
    {
        Service = service;
    }

    private AchievementService Service { get; }

    /// <summary>
    ///     Suggests achievements whose name or key contains the input. Hidden ones only show for staff.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The suggestions.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        if (context.Guild is null || autocompleteInteraction.User is not IGuildUser user)
            return AutocompletionResult.FromSuccess();

        var staff = user.GuildPermissions.ManageGuild;
        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";
        var catalog = await Service.GetCatalogAsync(context.Guild.Id);

        var suggestions = catalog.All
            .Where(d => !d.IsGlobal && (staff || !d.Hidden))
            .Where(d => d.Name.Contains(input, StringComparison.OrdinalIgnoreCase) ||
                        d.Key.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .Select(d => new AutocompleteResult(
                $"{d.Name} ({catalog.Category(d.CategoryKey).Name})".TrimTo(MaxNameLength), d.Key));

        return AutocompletionResult.FromSuccess(suggestions);
    }
}

/// <summary>
///     Suggests metrics server made achievements can count.
/// </summary>
public class AchievementMetricAutocompleter : AutocompleteHandler
{
    /// <summary>
    ///     Suggests metrics whose label contains the input.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The suggestions.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";
        var suggestions = AchievementCatalog.Metrics
            .Where(m => m.AllowCustom)
            .Where(m => m.Label.Contains(input, StringComparison.OrdinalIgnoreCase) ||
                        m.Description.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(m => new AutocompleteResult($"{m.Label}: {m.Description}".TrimTo(100), m.Metric.ToString()));
        return Task.FromResult(AutocompletionResult.FromSuccess(suggestions));
    }
}

/// <summary>
///     Suggests a server's achievement categories.
/// </summary>
public class AchievementCategoryAutocompleter : AutocompleteHandler
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementCategoryAutocompleter" /> class.
    /// </summary>
    /// <param name="service">The achievement service.</param>
    public AchievementCategoryAutocompleter(AchievementService service)
    {
        Service = service;
    }

    private AchievementService Service { get; }

    /// <summary>
    ///     Suggests categories whose name contains the input.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The suggestions.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        if (context.Guild is null)
            return AutocompletionResult.FromSuccess();

        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";
        var catalog = await Service.GetCatalogAsync(context.Guild.Id);
        var suggestions = catalog.Categories
            .Where(c => c.Key != AchievementCatalog.GlobalCategory)
            .Where(c => c.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(c => new AutocompleteResult(c.Name.TrimTo(100), c.Key));
        return AutocompletionResult.FromSuccess(suggestions);
    }
}

/// <summary>
///     Suggests every badge a server offers.
/// </summary>
public class AchievementBadgeAutocompleter : AutocompleteHandler
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementBadgeAutocompleter" /> class.
    /// </summary>
    /// <param name="service">The achievement service.</param>
    public AchievementBadgeAutocompleter(AchievementService service)
    {
        Service = service;
    }

    private AchievementService Service { get; }

    /// <summary>
    ///     Whether to suggest only badges the caller owns.
    /// </summary>
    protected virtual bool OwnedOnly => false;

    /// <summary>
    ///     Suggests badges whose name or key contains the input.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The suggestions.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        if (context.Guild is null)
            return AutocompletionResult.FromSuccess();

        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";
        var badges = OwnedOnly
            ? await Service.GetOwnedBadgesAsync(context.Guild.Id, context.User.Id)
            : AchievementService.AllBadges(await Service.GetCatalogAsync(context.Guild.Id));

        var suggestions = badges
            .Where(b => b.Name.Contains(input, StringComparison.OrdinalIgnoreCase) ||
                        b.Key.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(b => new AutocompleteResult(
                $"{b.Name} ({AchievementCatalog.GetGrade(b.Grade).Name})".TrimTo(100), b.Key));
        return AutocompletionResult.FromSuccess(suggestions);
    }
}

/// <summary>
///     Suggests only the badges the caller owns.
/// </summary>
/// <param name="service">The achievement service.</param>
public class AchievementOwnedBadgeAutocompleter(AchievementService service) : AchievementBadgeAutocompleter(service)
{
    /// <inheritdoc />
    protected override bool OwnedOnly => true;
}

/// <summary>
///     Suggests achievement icons: the server's own emojis first, then icon names.
/// </summary>
public class AchievementIconAutocompleter : AutocompleteHandler
{
    /// <summary>
    ///     Suggests server emojis and icon names that contain the input.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The suggestions.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var input = (autocompleteInteraction.Data.Current.Value?.ToString() ?? "").Trim().ToLowerInvariant();
        var emojis = (context.Guild?.Emotes ?? [])
            .Where(e => e.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(e => new AutocompleteResult($":{e.Name}:", e.ToString()));
        var glyphs = AchievementIcons.Glyphs
            .Where(g => g.Name.Contains(input, StringComparison.Ordinal) ||
                        g.Aliases.Any(a => a.Contains(input, StringComparison.Ordinal)))
            .OrderByDescending(g => g.Name.StartsWith(input, StringComparison.Ordinal))
            .Take(25)
            .Select(g => new AutocompleteResult(g.Name, AchievementIcons.GlyphPrefix + g.Name));
        return Task.FromResult(AutocompletionResult.FromSuccess(emojis.Concat(glyphs).Take(25)));
    }
}
