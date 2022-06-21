using Mewdeko.Common.Configs;
using Mewdeko.Common.Yml;
using System.Text.RegularExpressions;
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

    public string Serialize<T>(T? obj)
    {
        try
        {
            var escapedOutput = _serializer.Serialize(obj);
            return _codePointRegex.Replace(escapedOutput,
                me =>
                {
                    var str = me.Groups["code"].Value;
                    return YamlHelper.UnescapeUnicodeCodePoint(str);
                });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public T Deserialize<T>(string data)
        => _deserializer.Deserialize<T>(data);
}