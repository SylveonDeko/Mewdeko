using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mewdeko.Core._Extensions
{
    public class Detection
    {
        [JsonProperty("bounding_box")] public List<int> BoundingBox { get; set; }

        [JsonProperty("confidence")] public string Confidence { get; set; }

        [JsonProperty("name")] public string Name { get; set; }
    }

    public class Output
    {
        [JsonProperty("detections")] public List<Detection> Detections { get; set; }

        [JsonProperty("nsfw_score")] public double NsfwScore { get; set; }
    }

    public class DeepAI
    {
        [JsonProperty("job_id")] public int JobId { get; set; }

        [JsonProperty("output")] public Output Output { get; set; }

        [JsonProperty("output_url")] public object OutputUrl { get; set; }
    }
}