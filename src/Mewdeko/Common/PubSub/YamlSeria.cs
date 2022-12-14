using System.Text.RegularExpressions;
using Mewdeko.Common.Configs;
using Mewdeko.Common.Yml;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.PubSub;

public class YamlSeria : IConfigSeria
{
    private static readonly Regex CodePointRegex =
        new(@"(\\U(?<code>[a-zA-Z0-9]{8})|\\u(?<code>[a-zA-Z0-9]{4})|\\x(?<code>[a-zA-Z0-9]{2}))",
            RegexOptions.Compiled);

    private readonly IDeserializer deserializer;
    private readonly ISerializer serializer;

    public YamlSeria()
    {
        serializer = Yaml.Serializer;
        deserializer = Yaml.Deserializer;
    }

    public string Serialize<T>(T? obj)
    {
        try
        {
            var escapedOutput = serializer.Serialize(obj);
            return CodePointRegex.Replace(escapedOutput,
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
        => deserializer.Deserialize<T>(data);
}