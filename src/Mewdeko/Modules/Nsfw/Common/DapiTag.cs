using Newtonsoft.Json;

namespace Mewdeko.Modules.Nsfw.Common;

public readonly struct DapiTag
{
    public string Name { get; }

    [JsonConstructor]
    public DapiTag(string name)
        => Name = name;
}