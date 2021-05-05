// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
using Newtonsoft.Json;
using System.Collections.Generic;
namespace Mewdeko.Extensions
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class Doc
    {
        [JsonProperty("filename")]
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        public string? Filename { get; set; }
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

        [JsonProperty("episode")]
        public int? Episode { get; set; }

        [JsonProperty("from")]
        public double? From { get; set; }

        [JsonProperty("to")]
        public double? To { get; set; }

        [JsonProperty("similarity")]
        public double? Similarity { get; set; }

        [JsonProperty("anilist_id")]
        public int? AnilistId { get; set; }

        [JsonProperty("anime")]
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        public string? Anime { get; set; }
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

        [JsonProperty("at")]
        public double? At { get; set; }

        [JsonProperty("is_adult")]
        public bool IsAdult { get; set; }

        [JsonProperty("mal_id")]
        public int? MalId { get; set; }

        [JsonProperty("season")]
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        public string? Season { get; set; }
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

        [JsonProperty("synonyms")]
        public List<string> Synonyms { get; set; }

        [JsonProperty("synonyms_chinese")]
        public List<string> SynonymsChinese { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("title_chinese")]
        public string TitleChinese { get; set; }

        [JsonProperty("title_english")]
        public string TitleEnglish { get; set; }

        [JsonProperty("title_native")]
        public string TitleNative { get; set; }

        [JsonProperty("title_romaji")]
        public string TitleRomaji { get; set; }

        [JsonProperty("tokenthumb")]
        public string Tokenthumb { get; set; }
    }

    public class Root
    {
        [JsonProperty("RawDocsCount")]
        public int RawDocsCount { get; set; }

        [JsonProperty("CacheHit")]
        public bool CacheHit { get; set; }

        [JsonProperty("trial")]
        public int Trial { get; set; }

        [JsonProperty("limit")]
        public int Limit { get; set; }

        [JsonProperty("limit_ttl")]
        public int LimitTtl { get; set; }

        [JsonProperty("quota")]
        public int Quota { get; set; }

        [JsonProperty("quota_ttl")]
        public int QuotaTtl { get; set; }

        [JsonProperty("RawDocsSearchTime")]
        public int RawDocsSearchTime { get; set; }

        [JsonProperty("ReRankSearchTime")]
        public int ReRankSearchTime { get; set; }

        [JsonProperty("docs")]
        public List<Doc> Docs { get; set; }
    }


}
