using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Mewdeko.Common.JsonSettings;

public class OrderedResolver : DefaultContractResolver
{
    protected override IList<JsonProperty> CreateProperties(System.Type type, MemberSerialization memberSerialization)
    {
        return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToList();
    }
}