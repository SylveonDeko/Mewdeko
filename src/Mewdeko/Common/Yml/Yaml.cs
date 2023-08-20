using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mewdeko.Common.Yml;

public class Yaml
{
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

    public static IDeserializer Deserializer => new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(new SkColorConverter())
        .WithTypeConverter(new CultureInfoConverter())
        .WithTypeConverter(new UriConverter())
        .Build();
}