using System.Globalization;
using SixLabors.ImageSharp.PixelFormats;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Yml;

public class Rgba32Converter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(Rgba32);

    public object ReadYaml(IParser parser, Type type)
    {
        var scalar = parser.Consume<Scalar>();
        return Rgba32.ParseHex(scalar.Value);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        var color = (Rgba32)value;
        var val = (uint)((color.B << 0) | (color.G << 8) | (color.R << 16));
        emitter.Emit(new Scalar(val.ToString("X6").ToLower()));
    }
}

public class CultureInfoConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(CultureInfo);

    public object ReadYaml(IParser parser, Type type)
    {
        var scalar = parser.Consume<Scalar>();
        return new CultureInfo(scalar.Value);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        var ci = (CultureInfo)value;
        emitter.Emit(new Scalar(ci.Name));
    }
}