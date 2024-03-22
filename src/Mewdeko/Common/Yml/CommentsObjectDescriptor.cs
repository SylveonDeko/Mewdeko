using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Mewdeko.Common.Yml
{
    /// <summary>
    /// Represents an object descriptor with associated comments during YAML serialization.
    /// </summary>
    public sealed class CommentsObjectDescriptor : IObjectDescriptor
    {
        private readonly IObjectDescriptor innerDescriptor;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommentsObjectDescriptor"/> class.
        /// </summary>
        /// <param name="innerDescriptor">The inner object descriptor.</param>
        /// <param name="comment">The comment associated with the object.</param>
        public CommentsObjectDescriptor(IObjectDescriptor innerDescriptor, string comment)
        {
            this.innerDescriptor = innerDescriptor;
            Comment = comment;
        }

        /// <summary>
        /// Gets the comment associated with the object.
        /// </summary>
        public string Comment { get; }

        /// <inheritdoc/>
        public object Value => innerDescriptor.Value;

        /// <inheritdoc/>
        public Type Type => innerDescriptor.Type;

        /// <inheritdoc/>
        public Type StaticType => innerDescriptor.StaticType;

        /// <inheritdoc/>
        public ScalarStyle ScalarStyle => innerDescriptor.ScalarStyle;
    }
}