using Newtonsoft.Json;

namespace Mewdeko.Modules.Nsfw.Common
{
    /// <summary>
    /// Represents a tag retrieved from a DAPI (Danbooru API) endpoint.
    /// </summary>
    public readonly struct DapiTag
    {
        /// <summary>
        /// Gets the name of the tag.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DapiTag"/> struct with the specified name.
        /// </summary>
        /// <param name="name">The name of the tag.</param>
        [JsonConstructor]
        public DapiTag(string name)
            => Name = name;
    }
}