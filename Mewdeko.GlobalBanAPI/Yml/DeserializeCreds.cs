using YamlDotNet.Serialization;

namespace Mewdeko.GlobalBanAPI.Yml;

public class DeserializeYaml
{
    public static Credentials CredsDeserialize()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "config.yml");
        var file = File.ReadAllText(path);
        var deserializer = new Deserializer();
        var creds = deserializer.Deserialize<Credentials>(file);
        return creds;
    }
}