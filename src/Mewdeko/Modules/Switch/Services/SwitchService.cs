using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mewdeko.Modules.Switch.Common;

namespace Mewdeko.Modules.Switch.Services;

/// <summary>
///     Provides Nintendo Switch error code lookups, switchbrew.org wiki search, and Ryujinx log analysis.
/// </summary>
/// <param name="httpFactory">The HTTP client factory used for switchbrew lookups and log downloads.</param>
public class SwitchService(IHttpClientFactory httpFactory) : INService
{
    private const string SwitchbrewApiUrl = "https://switchbrew.org/w/api.php";
    private static readonly Regex SwitchCodeRegex = new(@"^2\d{3}-\d{4}$", RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled);

    /// <summary>
    ///     Attempts to resolve a Nintendo Switch error code, accepting either the <c>NNNN-NNNN</c> format or a
    ///     hexadecimal value (e.g. <c>0x2A2</c>).
    /// </summary>
    /// <param name="input">The error code to resolve.</param>
    /// <returns>The resolved error, or <c>null</c> if the input isn't in a recognizable Switch error code format.</returns>
    public SwitchErrorLookup? ResolveSwitchError(string input)
    {
        int module, description, errorCode;

        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(input[2..], NumberStyles.HexNumber, null, out var raw))
                return null;

            module = raw & 0x1FF;
            description = (raw >> 9) & 0x3FFF;
            errorCode = raw;
        }
        else if (SwitchCodeRegex.IsMatch(input))
        {
            module = int.Parse(input[..4]) - 2000;
            description = int.Parse(input[5..9]);
            errorCode = (description << 9) + module;
        }
        else
        {
            return null;
        }

        var errorCodeString = $"{module + 2000:D4}-{description:D4}";
        var moduleName = SwitchErrorData.SwitchModules.GetValueOrDefault(module);

        var errorDescription = string.Empty;
        var isKnown = false;

        if (SwitchErrorData.SwitchKnownErrorCodes.TryGetValue(errorCode, out var known))
        {
            errorDescription = known;
            isKnown = true;
        }
        else if (SwitchErrorData.SwitchSupportPage.TryGetValue(errorCodeString, out var supportPage))
        {
            errorDescription = supportPage;
            isKnown = true;
        }
        else if (SwitchErrorData.SwitchKnownErrorCodeRanges.TryGetValue(module, out var ranges))
        {
            foreach (var (low, high, desc) in ranges)
            {
                if (description < low || description > high)
                    continue;

                errorDescription = desc;
                isKnown = true;
                break;
            }
        }

        return new SwitchErrorLookup(errorCodeString, $"0x{errorCode:X}", module, moduleName, description,
            errorDescription, isKnown);
    }

    /// <summary>
    ///     Attempts to resolve a special-case Switch error code that doesn't follow the standard NNNN-NNNN format.
    /// </summary>
    /// <param name="input">The error code to resolve.</param>
    public SwitchGameErrorLookup? ResolveSwitchGameError(string input)
    {
        if (!SwitchErrorData.SwitchGameErrors.TryGetValue(input, out var raw))
            return null;

        var parts = raw.Split(':', 2);
        return new SwitchGameErrorLookup(parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : string.Empty);
    }

    /// <summary>
    ///     Converts a Switch error code in <c>NNNN-NNNN</c> format to its hexadecimal representation.
    /// </summary>
    /// <param name="input">The error code to convert.</param>
    /// <returns>The hexadecimal value, or <c>null</c> if the input isn't in the standard format.</returns>
    public int? SwitchErrorToHex(string input)
    {
        if (!SwitchCodeRegex.IsMatch(input))
            return null;

        var module = int.Parse(input[..4]) - 2000;
        var description = int.Parse(input[5..9]);
        return (description << 9) + module;
    }

    /// <summary>
    ///     Converts a hexadecimal Switch error code to its <c>NNNN-NNNN</c> representation.
    /// </summary>
    /// <param name="input">The hexadecimal error code to convert, with or without the <c>0x</c> prefix.</param>
    /// <returns>The formatted error code, or <c>null</c> if the input isn't valid hexadecimal.</returns>
    public string? HexToSwitchError(string input)
    {
        var trimmed = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? input[2..] : input;
        if (!int.TryParse(trimmed, NumberStyles.HexNumber, null, out var raw))
            return null;

        var module = raw & 0x1FF;
        var description = (raw >> 9) & 0x3FFF;
        return $"{module + 2000:D4}-{description:D4}";
    }

    /// <summary>
    ///     Searches the switchbrew.org wiki and returns the top matching pages.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    public async Task<List<SwitchbrewSearchResult>> SearchSwitchbrewAsync(string query, int limit = 5)
    {
        using var http = httpFactory.CreateClient();
        var url = $"{SwitchbrewApiUrl}?action=query&list=search&format=json&srlimit={limit}" +
                  $"&srsearch={Uri.EscapeDataString(query)}";

        await using var stream = await http.GetStreamAsync(url).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<SwitchbrewSearchResponse>(stream).ConfigureAwait(false);

        var results = data?.Query?.Search ?? [];
        foreach (var result in results)
            result.Snippet = WebUtility.HtmlDecode(HtmlTagRegex.Replace(result.Snippet, string.Empty));

        return results;
    }

    /// <summary>
    ///     Downloads the first and last portion of a log file, mirroring the bounded read used by ryuko-ng to avoid
    ///     abuse from oversized uploads.
    /// </summary>
    /// <param name="url">The direct URL of the attachment to download.</param>
    public async Task<string> DownloadLogAsync(string url)
    {
        using var http = httpFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue();
        request.Headers.Range.Ranges.Add(new RangeItemHeaderValue(0, 60_000));
        request.Headers.Range.Ranges.Add(new RangeItemHeaderValue(null, 6_000));

        using var response = await http.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Analyses a Ryujinx log file's contents.
    /// </summary>
    /// <param name="logText">The raw contents of the log file.</param>
    public RyujinxLogAnalysis AnalyseLog(string logText)
    {
        return new RyujinxLogAnalyser(logText).Analyse();
    }
}