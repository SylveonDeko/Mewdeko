using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mewdeko.Common.Yml
{
    /// <summary>
    /// Provides static methods for obtaining YAML serializer and deserializer instances with custom configurations.
    /// </summary>
    public class Yaml
    {
        /// <summary>
        /// Gets a YAML serializer instance configured with custom settings.
        /// </summary>
        public static ISerializer Serializer => new SerializerBuilder()
            .WithTypeInspector(inner => new CommentGatheringTypeInspector(inner))
            .WithEmissionPhaseObjectGraphVisitor(args => new CommentsObjectGraphVisitor(args.InnerVisitor))
            .WithEventEmitter(args => new MultilineScalarFlowStyleEmitter(args))
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithIndentedSequences()
            .WithTypeConverter(new SkColorConverter())
            .WithTypeConverter(new CultureInfoConverter())
            .WithTypeConverter(new UriConverter())
            .Build();

        /// <summary>
        /// Gets a YAML deserializer instance configured with custom settings.
        /// </summary>
        public static IDeserializer Deserializer => new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new SkColorConverter())
            .WithTypeConverter(new CultureInfoConverter())
            .WithTypeConverter(new UriConverter())
            .Build();
    }
}