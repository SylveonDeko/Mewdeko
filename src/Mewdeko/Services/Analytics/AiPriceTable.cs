namespace Mewdeko.Services.Analytics;

/// <summary>
///     List prices in USD per million tokens for the models the bot can be configured with.
/// </summary>
public static class AiPriceTable
{
    private static readonly Dictionary<string, (double In, double Out)> Prices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-4o"] = (2.50, 10.00),
        ["gpt-4o-mini"] = (0.15, 0.60),
        ["gpt-4.1"] = (2.00, 8.00),
        ["gpt-4.1-mini"] = (0.40, 1.60),
        ["gpt-4.1-nano"] = (0.10, 0.40),
        ["gpt-4-turbo"] = (10.00, 30.00),
        ["gpt-4"] = (30.00, 60.00),
        ["gpt-3.5-turbo"] = (0.50, 1.50),
        ["gpt-5"] = (1.25, 10.00),
        ["gpt-5-mini"] = (0.25, 2.00),
        ["gpt-5-nano"] = (0.05, 0.40),
        ["o1"] = (15.00, 60.00),
        ["o1-mini"] = (1.10, 4.40),
        ["o3"] = (2.00, 8.00),
        ["o3-mini"] = (1.10, 4.40),
        ["o4-mini"] = (1.10, 4.40),
        ["claude-opus-4-1"] = (15.00, 75.00),
        ["claude-opus-4"] = (15.00, 75.00),
        ["claude-sonnet-4-5"] = (3.00, 15.00),
        ["claude-sonnet-4"] = (3.00, 15.00),
        ["claude-3-7-sonnet"] = (3.00, 15.00),
        ["claude-3-5-sonnet"] = (3.00, 15.00),
        ["claude-3-5-haiku"] = (0.80, 4.00),
        ["claude-haiku-4-5"] = (1.00, 5.00),
        ["claude-3-opus"] = (15.00, 75.00),
        ["claude-3-haiku"] = (0.25, 1.25),
        ["llama-3.3-70b-versatile"] = (0.59, 0.79),
        ["llama-3.1-8b-instant"] = (0.05, 0.08),
        ["llama-3.1-70b-versatile"] = (0.59, 0.79),
        ["llama3-70b-8192"] = (0.59, 0.79),
        ["llama3-8b-8192"] = (0.05, 0.08),
        ["mixtral-8x7b-32768"] = (0.24, 0.24),
        ["gemma2-9b-it"] = (0.20, 0.20),
        ["deepseek-r1-distill-llama-70b"] = (0.75, 0.99),
        ["qwen-qwq-32b"] = (0.29, 0.39)
    };

    /// <summary>
    ///     Estimates the cost of a token count for a model.
    /// </summary>
    /// <param name="model">The model name as reported by the provider.</param>
    /// <param name="tokensIn">Prompt tokens.</param>
    /// <param name="tokensOut">Completion tokens.</param>
    /// <returns>The estimated cost in USD, or null when the model is not priced.</returns>
    public static double? Estimate(string model, double tokensIn, double tokensOut)
    {
        var price = Lookup(model);
        if (price is null) return null;
        return (tokensIn * price.Value.In + tokensOut * price.Value.Out) / 1_000_000d;
    }

    private static (double In, double Out)? Lookup(string model)
    {
        if (string.IsNullOrEmpty(model)) return null;
        if (Prices.TryGetValue(model, out var exact)) return exact;

        (double In, double Out)? best = null;
        var bestLength = 0;
        foreach (var (name, price) in Prices)
        {
            if (!model.StartsWith(name, StringComparison.OrdinalIgnoreCase) || name.Length <= bestLength) continue;
            best = price;
            bestLength = name.Length;
        }

        return best;
    }
}