using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mewdeko.Extensions
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class Language
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("url")] public string Url { get; set; }
    }

    public class EffectEntry
    {
        [JsonProperty("effect")] public string Effect { get; set; }

        [JsonProperty("language")] public Language Language { get; set; }

        [JsonProperty("short_effect")] public string ShortEffect { get; set; }
    }

    public class VersionGroupAbility
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("url")] public string Url { get; set; }
    }

    public class FlavorTextEntry
    {
        [JsonProperty("flavor_text")] public string FlavorText { get; set; }

        [JsonProperty("language")] public Language Language { get; set; }

        [JsonProperty("version_group")] public VersionGroupAbility VersionGroup { get; set; }
    }

    public class Generation
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("url")] public string Url { get; set; }
    }

    public class Name
    {
        [JsonProperty("language")] public Language Language { get; set; }

        [JsonProperty("name")] public string name { get; set; }
    }

    public class Pokemon2
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("url")] public string Url { get; set; }

        [JsonProperty("is_hidden")] public bool IsHidden { get; set; }

        [JsonProperty("pokemon")] public Pokemon2 Pokemon { get; set; }

        [JsonProperty("slot")] public int Slot { get; set; }
    }

    public class PokeAbility
    {
        [JsonProperty("effect_changes")] public List<object> EffectChanges { get; set; }

        [JsonProperty("effect_entries")] public List<EffectEntry> EffectEntries { get; set; }

        [JsonProperty("flavor_text_entries")] public List<FlavorTextEntry> FlavorTextEntries { get; set; }

        [JsonProperty("generation")] public Generation Generation { get; set; }

        [JsonProperty("id")] public int Id { get; set; }

        [JsonProperty("is_main_series")] public bool IsMainSeries { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("names")] public List<Name> Names { get; set; }

        [JsonProperty("pokemon")] public List<Pokemon2> Pokemon { get; set; }
    }
}