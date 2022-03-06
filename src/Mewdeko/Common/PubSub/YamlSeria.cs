using System.Text.RegularExpressions;
using Mewdeko.Common.Configs;
using Mewdeko.Common.Yml;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.PubSub;

public class YamlSeria : IConfigSeria
{
    private static readonly Regex _codePointRegex =
        new(@"(\\U(?<code>[a-zA-Z0-9]{8})|\\u(?<code>[a-zA-Z0-9]{4})|\\x(?<code>[a-zA-Z0-9]{2}))",
            RegexOptions.Compiled);

    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public YamlSeria()
    {
        _serializer = Yaml.Serializer;
        _deserializer = Yaml.Deserializer;
    }

#pragma warning disable CS8633
    public string Serialize<T>(T obj)
#pragma warning restore CS8633
        where T : notnull
    {
        var escapedOutput = _serializer.Serialize(obj);
        var output = _codePointRegex.Replace(escapedOutput,
            me =>
            {
                var str = me.Groups["code"].Value;
                var newString = YamlHelper.UnescapeUnicodeCodePoint(str);
                return newString;
            });
        return output;
    }

    public T Deserialize<T>(string data)
        => _deserializer.Deserialize<T>(data);

}