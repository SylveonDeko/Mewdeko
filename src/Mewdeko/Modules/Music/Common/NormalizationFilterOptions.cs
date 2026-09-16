using System.Text.Json;
using Lavalink4NET.Filters;
using Lavalink4NET.Protocol.Models.Filters;

namespace Mewdeko.Modules.Music.Common;

/// <summary>
///     Volume normalization filter provided by the LavaDSPX Lavalink plugin. Requires the plugin to be installed on
///     the node; nodes without it ignore the filter.
/// </summary>
/// <param name="MaxAmplitude">The peak amplitude to normalize to, between 0 and 1.</param>
/// <param name="Adaptive">Whether peak amplitudes persist across the track rather than being measured per chunk.</param>
public sealed record NormalizationFilterOptions(float? MaxAmplitude = null, bool? Adaptive = null) : IFilterOptions
{
    /// <summary>
    ///     The key the plugin reads the filter from in the Lavalink filter map.
    /// </summary>
    public const string FilterName = "normalization";

    /// <inheritdoc />
    public bool IsDefault
    {
        get
        {
            return MaxAmplitude is null && Adaptive is null;
        }
    }

    /// <inheritdoc />
    public void Apply(ref PlayerFilterMapModel filterMap)
    {
        var additional = filterMap.AdditionalFilters is null
            ? new Dictionary<string, JsonElement>()
            : new Dictionary<string, JsonElement>(filterMap.AdditionalFilters);
        if (IsDefault)
        {
            additional.Remove(FilterName);
        }
        else
        {
            var payload = new Dictionary<string, object>();
            if (MaxAmplitude is not null) payload["maxAmplitude"] = MaxAmplitude.Value;
            if (Adaptive is not null) payload["adaptive"] = Adaptive.Value;
            additional[FilterName] = JsonSerializer.SerializeToElement(payload);
        }

        filterMap = filterMap with
        {
            AdditionalFilters = additional
        };
    }
}