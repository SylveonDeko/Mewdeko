using Newtonsoft.Json;

namespace Mewdeko.Extensions;

public class Result
{
    [JsonProperty("anilist")]
    public int Anilist { get; set; }

    [JsonProperty("filename")]
    public string Filename { get; set; }

    [JsonProperty("episode")]
    public double Episode { get; set; }

    [JsonProperty("from")]
    public double From { get; set; }

    [JsonProperty("to")]
    public double To { get; set; }

    [JsonProperty("similarity")]
    public double Similarity { get; set; }

    [JsonProperty("video")]
    public string Video { get; set; }

    [JsonProperty("image")]
    public string Image { get; set; }
}

public class MoeResponse
{
    [JsonProperty("frameCount")]
    public int FrameCount { get; set; }

    [JsonProperty("error")]
    public string Error { get; set; }

    [JsonProperty("result")]
    public List<Result> Result { get; set; }
}