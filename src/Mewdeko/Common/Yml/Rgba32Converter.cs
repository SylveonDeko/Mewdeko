using System.Globalization;
using SkiaSharp;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Yml;

public class SkColorConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(SKColor);

    public object ReadYaml(IParser parser, Type type)
    {
        var scalar = parser.Consume<Scalar>();
        return SKColor.Parse(scalar.Value);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        var color = (SKColor)value;
        var val = (uint)((color.Blue << 0) | (color.Green << 8) | (color.Red << 16));
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