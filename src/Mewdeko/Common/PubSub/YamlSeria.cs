using System.Text.RegularExpressions;
using Mewdeko.Common.Configs;
using Mewdeko.Common.Yml;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.PubSub
{
    /// <summary>
    /// Class that implements the IConfigSeria interface for YAML serialization and deserialization.
    /// </summary>
    public partial class YamlSeria : IConfigSeria
    {
        /// <summary>
        /// Regular expression for matching Unicode code points.
        /// </summary>
        private static readonly Regex CodePointRegex = MyRegex();

        /// <summary>
        /// The YAML deserializer.
        /// </summary>
        private readonly IDeserializer deserializer;

        /// <summary>
        /// The YAML serializer.
        /// </summary>
        private readonly ISerializer serializer;

        /// <summary>
        /// Lock object for thread-safe operations.
        /// </summary>
        private readonly object lockObj = new object();

        /// <summary>
        /// Initializes a new instance of the YamlSeria class.
        /// </summary>
        public YamlSeria()
        {
            serializer = Yaml.Serializer;
            deserializer = Yaml.Deserializer;
        }

        /// <summary>
        /// Serializes the given object into a YAML string.
        /// </summary>
        /// <typeparam name="T">The type of object to serialize.</typeparam>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A YAML string representing the serialized object.</returns>
        public string Serialize<T>(T obj)
        {
            lock (lockObj)
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
        }

        /// <summary>
        /// Deserializes the given YAML string into an object of type T.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize into.</typeparam>
        /// <param name="data">The YAML string to deserialize.</param>
        /// <returns>An object of type T representing the deserialized data.</returns>
        public T Deserialize<T>(string data)
        {
            lock (lockObj)
            {
                return deserializer.Deserialize<T>(data);
            }
        }

        /// <summary>
        /// Generates a regular expression for matching Unicode code points.
        /// </summary>
        /// <returns>A compiled regular expression.</returns>
        [GeneratedRegex("(\\\\U(?<code>[a-zA-Z0-9]{8})|\\\\u(?<code>[a-zA-Z0-9]{4})|\\\\x(?<code>[a-zA-Z0-9]{2}))", RegexOptions.Compiled)]
        private static partial Regex MyRegex();
    }
}