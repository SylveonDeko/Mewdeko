using YamlDotNet.Serialization;

namespace NadekoBot.Common.Yml
{
    public class Yaml
    {
        public static ISerializer Serializer => new SerializerBuilder()
            .WithTypeInspector(inner => new CommentGatheringTypeInspector(inner))
            .WithEmissionPhaseObjectGraphVisitor(args => new CommentsObjectGraphVisitor(args.InnerVisitor))
            .WithEventEmitter(args => new MultilineScalarFlowStyleEmitter(args))
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new Rgba32Converter())
            .WithTypeConverter(new CultureInfoConverter())
            .WithTypeConverter(new UriConverter())
            .Build();

        public static IDeserializer Deserializer => new DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new Rgba32Converter())
            .WithTypeConverter(new CultureInfoConverter())
            .WithTypeConverter(new UriConverter())
            .Build();
    }
}