using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Mewdeko.Common.JsonSettings;

public class OrderedResolver : DefaultContractResolver
{
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToList();
    }
}

public class LowercaseContractResolver : DefaultContractResolver
{
    protected override string ResolvePropertyName(string propertyName)
    {
        return propertyName.ToLower();
    }
}