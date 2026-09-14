using Mewdeko.Modules.Patreon.Services;

namespace Mewdeko.Modules.Patreon.Extensions;

/// <summary>
///     Extension methods for ReplacementBuilder to add Patreon-specific placeholders
/// </summary>
public static class ReplacementBuilderExtensions
{
    /// <summary>
    ///     Adds Patreon-related placeholders to the ReplacementBuilder
    /// </summary>
    /// <param name="builder">The ReplacementBuilder instance</param>
    /// <param name="analytics">Patreon analytics data, can be null</param>
    /// <returns>The ReplacementBuilder instance for chaining</returns>
    public static ReplacementBuilder WithPatreonData(this ReplacementBuilder builder, PatreonAnalytics? analytics)
    {
        // Supporter count placeholders
        builder.WithOverride("%supporter.count%", () => (analytics?.ActiveSupporters ?? 0).ToString());
        builder.WithOverride("%supporter.total%", () => (analytics?.TotalSupporters ?? 0).ToString());
        builder.WithOverride("%supporter.new%", () => (analytics?.NewSupportersThisMonth ?? 0).ToString());
        builder.WithOverride("%supporter.former%", () => (analytics?.FormerSupporters ?? 0).ToString());
        builder.WithOverride("%supporter.linked%", () => (analytics?.LinkedSupporters ?? 0).ToString());

        // Revenue placeholders
        builder.WithOverride("%revenue.monthly%", () => $"{analytics?.TotalMonthlyRevenue ?? 0:F2}");
        builder.WithOverride("%revenue.average%", () => $"{analytics?.AverageSupport ?? 0:F2}");
        builder.WithOverride("%revenue.lifetime%", () => $"{analytics?.LifetimeRevenue ?? 0:F2}");

        // Goal and growth placeholders
        builder.WithOverride("%growth.new%", () => (analytics?.NewSupportersThisMonth ?? 0).ToString());

        // Top supporter (privacy-aware)
        var topSupporter = analytics?.TopSupporters?.FirstOrDefault();
        builder.WithOverride("%top.supporter.name%", () => topSupporter?.Name ?? "Anonymous Supporter");
        builder.WithOverride("%top.supporter.amount%", () => $"{topSupporter?.Amount ?? 0:F2}");
        builder.WithOverride("%patron.name%", () => topSupporter?.Name ?? "Anonymous Supporter");
        builder.WithOverride("%patron.tier%", () => topSupporter?.Tier ?? "Unknown");
        builder.WithOverride("%patron.amount%", () => $"{topSupporter?.Amount ?? 0:F2}");

        // Tier distribution (simplified)
        var totalTiers = analytics?.TierDistribution?.Count ?? 0;
        builder.WithOverride("%tiers.count%", () => totalTiers.ToString());

        // Popular tier
        var popularTier = analytics?.TierDistribution?.OrderByDescending(x => x.Value).FirstOrDefault();
        builder.WithOverride("%tier.popular%", () => popularTier?.Key ?? "Unknown");
        builder.WithOverride("%tier.popular.count%", () => (popularTier?.Value ?? 0).ToString());

        // Formatted strings for announcements
        builder.WithOverride("%supporters.summary%", () =>
        {
            if (analytics == null || analytics.ActiveSupporters == 0)
                return "our amazing community";

            return analytics.ActiveSupporters == 1
                ? "our 1 incredible supporter"
                : $"our {analytics.ActiveSupporters} incredible supporters";
        });

        builder.WithOverride("%revenue.summary%", () =>
        {
            if (analytics == null || analytics.TotalMonthlyRevenue == 0)
                return "";

            return $" who help us raise ${analytics.TotalMonthlyRevenue:F0}/month";
        });

        builder.WithOverride("%growth.summary%", () =>
        {
            if (analytics == null || analytics.NewSupportersThisMonth == 0)
                return "";

            return analytics.NewSupportersThisMonth == 1
                ? " We gained 1 new supporter this month!"
                : $" We gained {analytics.NewSupportersThisMonth} new supporters this month!";
        });

        return builder;
    }
}