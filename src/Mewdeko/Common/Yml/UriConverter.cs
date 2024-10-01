using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Yml;

/// <summary>
///     YamlDotNet type converter for serializing and deserializing Uri objects.
/// </summary>
public class UriConverter : IYamlTypeConverter
{
    /// <inheritdoc />
    public bool Accepts(Type type)
    {
        return type == typeof(Uri);
    }

    /// <inheritdoc />
    public object ReadYaml(IParser parser, Type type)
    {
        var scalar = parser.Consume<Scalar>();
        return new Uri(scalar.Value);
    }

    /// <inheritdoc />
    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        var uri = (Uri)value;
        emitter.Emit(new Scalar(uri.ToString()));
    }
}