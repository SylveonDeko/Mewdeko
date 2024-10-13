using System.Globalization;
using SkiaSharp;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Yml;

/// <summary>
///     YamlDotNet type converter for serializing and deserializing SKColor objects.
/// </summary>
public class SkColorConverter : IYamlTypeConverter
{
    /// <inheritdoc />
    public bool Accepts(Type type)
    {
        return type == typeof(SKColor);
    }

    /// <inheritdoc />
    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return SKColor.Parse(scalar.Value);
    }

    /// <inheritdoc />
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var color = (SKColor)value;
        var val = (uint)((color.Blue << 0) | (color.Green << 8) | (color.Red << 16));
        emitter.Emit(new Scalar(val.ToString("X6").ToLower()));
    }
}

/// <summary>
///     YamlDotNet type converter for serializing and deserializing CultureInfo objects.
/// </summary>
public class CultureInfoConverter : IYamlTypeConverter
{
    /// <inheritdoc />
    public bool Accepts(Type type)
    {
        return type == typeof(CultureInfo);
    }

    /// <inheritdoc />
    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return new CultureInfo(scalar.Value);
    }

    /// <inheritdoc />
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var ci = (CultureInfo)value;
        emitter.Emit(new Scalar(ci.Name));
    }
}