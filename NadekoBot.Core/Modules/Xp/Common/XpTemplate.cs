using System;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NadekoBot.Core.Modules.Xp.Common
{
    public class XpTemplate
    {
        [JsonProperty("output_size")]
        public XpTemplatePos OutputSize { get; set; } = new XpTemplatePos
        {
            X = 450,
            Y = 220,
        };
        public XpTemplateUser User { get; set; } = new XpTemplateUser
        {
            Name = new XpTemplateText
            {
                FontSize = 50,
                Show = true,
                Pos = new XpTemplatePos
                {
                    X = 130,
                    Y = 17,
                }
            },
            Icon = new XpTemplateIcon
            {
                Show = true,
                Pos = new XpTemplatePos
                {
                    X = 32,
                    Y = 10,
                },
                Size = new XpTemplatePos
                {
                    X = 69,
                    Y = 70,
                }
            },
            GuildLevel = new XpTemplateText
            {
                Show = true,
                FontSize = 45,
                Pos = new XpTemplatePos
                {
                    X = 47,
                    Y = 297,
                }
            },
            GlobalLevel = new XpTemplateText
            {
                Show = true,
                FontSize = 45,
                Pos = new XpTemplatePos
                {
                    X = 47,
                    Y = 149,
                }
            },
            GuildRank = new XpTemplateText
            {
                Show = true,
                FontSize = 30,
                Pos = new XpTemplatePos
                {
                    X = 148,
                    Y = 326,
                }
            },
            GlobalRank = new XpTemplateText
            {
                Show = true,
                FontSize = 30,
                Pos = new XpTemplatePos
                {
                    X = 148,
                    Y = 179,
                }
            },
            TimeOnLevel = new XpTemplateTimeOnLevel
            {
                Format = "{0}d{1}h{2}m",
                Global = new XpTemplateText
                {
                    FontSize = 20,
                    Show = true,
                    Pos = new XpTemplatePos
                    {
                        X = 50,
                        Y = 204
                    }
                },
                Guild = new XpTemplateText
                {
                    FontSize = 20,
                    Show = true,
                    Pos = new XpTemplatePos
                    {
                        X = 50,
                        Y = 351
                    }
                }
            },
            Xp = new XpTemplateXP
            {
                Bar = new XpTemplateXpBar
                {
                    Show = true,
                    Global = new XpBar
                    {
                        Direction = XpTemplateDirection.Right,
                        Length = 450,
                        Color = new Rgba32(0, 0, 0, 0.4f),
                        PointA = new XpTemplatePos
                        {
                            X = 321,
                            Y = 104
                        },
                        PointB = new XpTemplatePos
                        {
                            X = 286,
                            Y = 235
                        }
                    },
                    Guild = new XpBar
                    {
                        Direction = XpTemplateDirection.Right,
                        Length = 450,
                        Color = new Rgba32(0, 0, 0, 0.4f),
                        PointA = new XpTemplatePos
                        {
                            X = 282,
                            Y = 248
                        },
                        PointB = new XpTemplatePos
                        {
                            X = 247,
                            Y = 379
                        }
                    }
                },
                Global = new XpTemplateText
                {
                    Show = true,
                    FontSize = 50,
                    Pos = new XpTemplatePos
                    {
                        X = 430,
                        Y = 142
                    }
                },
                Guild = new XpTemplateText
                {
                    Show = true,
                    FontSize = 50,
                    Pos = new XpTemplatePos
                    {
                        X = 400,
                        Y = 282
                    }
                },
                Awarded = new XpTemplateText
                {
                    Show = true,
                    FontSize = 25,
                    Pos = new XpTemplatePos
                    {
                        X = 445,
                        Y = 347
                    }
                }
            }
        };
        public XpTemplateClub Club { get; set; } = new XpTemplateClub
        {
            Icon = new XpTemplateIcon
            {
                Show = true,
                Pos = new XpTemplatePos
                {
                    X = 722,
                    Y = 25,
                },
                Size = new XpTemplatePos
                {
                    X = 45,
                    Y = 45,
                }
            },
            Name = new XpTemplateText
            {
                FontSize = 35,
                Pos = new XpTemplatePos
                {
                    X = 650,
                    Y = 49
                },
                Show = true,
            }
        };
    }

    public class XpTemplateIcon
    {
        public bool Show { get; set; }
        public XpTemplatePos Pos { get; set; }
        public XpTemplatePos Size { get; set; }
    }

    public class XpTemplatePos
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class XpTemplateUser
    {
        public XpTemplateText Name { get; set; }
        public XpTemplateIcon Icon { get; set; }
        public XpTemplateText GlobalLevel { get; set; }
        public XpTemplateText GuildLevel { get; set; }
        public XpTemplateText GlobalRank { get; set; }
        public XpTemplateText GuildRank { get; set; }
        public XpTemplateTimeOnLevel TimeOnLevel { get; set; }
        public XpTemplateXP Xp { get; set; }
    }

    public class XpTemplateTimeOnLevel
    {
        public string Format { get; set; }
        public XpTemplateText Global { get; set; }
        public XpTemplateText Guild { get; set; }
    }

    public class XpTemplateClub
    {
        public XpTemplateIcon Icon { get; set; }
        public XpTemplateText Name { get; set; }
    }

    public class XpTemplateText
    {
        [JsonConverter(typeof(XpRgba32Converter))]
        public Rgba32 Color { get; set; } = SixLabors.ImageSharp.Color.White;
        public bool Show { get; set; }
        public int FontSize { get; set; }
        public XpTemplatePos Pos { get; set; }
    }

    public class XpTemplateXP
    {
        public XpTemplateXpBar Bar { get; set; }
        public XpTemplateText Global { get; set; }
        public XpTemplateText Guild { get; set; }
        public XpTemplateText Awarded { get; set; }
    }

    public class XpTemplateXpBar
    {
        public bool Show { get; set; }
        public XpBar Global { get; set; }
        public XpBar Guild { get; set; }
    }

    public class XpBar
    {
        [JsonConverter(typeof(XpRgba32Converter))]
        public Rgba32 Color { get; set; }
        public XpTemplatePos PointA { get; set; }
        public XpTemplatePos PointB { get; set; }
        public int Length { get; set; }
        public XpTemplateDirection Direction { get; set; }
    }

    public enum XpTemplateDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public class XpRgba32Converter : JsonConverter<Rgba32>
    {
        public override Rgba32 ReadJson(JsonReader reader, Type objectType, Rgba32 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return Color.ParseHex(reader.Value.ToString());
        }

        public override void WriteJson(JsonWriter writer, Rgba32 value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToHex().ToLowerInvariant());
        }
    }
}
