using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Mewdeko.Common.JsonSettings;

/// <summary>
///     Provides a contract resolver that orders properties alphabetically.
/// </summary>
public class OrderedResolver : DefaultContractResolver
{
    /// <summary>
    ///     Creates properties for the specified <see cref="JsonContract" />.
    /// </summary>
    /// <param name="type">The type to create properties for.</param>
    /// <param name="memberSerialization">The member serialization.</param>
    /// <returns>A collection of properties for the specified contract.</returns>
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        // Order properties alphabetically
        return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToList();
    }
}

/// <summary>
///     Provides a contract resolver that resolves property names to lowercase.
/// </summary>
public class LowercaseContractResolver : DefaultContractResolver
{
    /// <summary>
    ///     Resolves the property name to lowercase.
    /// </summary>
    /// <param name="propertyName">The property name to resolve.</param>
    /// <returns>The resolved property name.</returns>
    protected override string ResolvePropertyName(string propertyName)
    {
        // Convert property name to lowercase
        return propertyName.ToLower();
    }
}