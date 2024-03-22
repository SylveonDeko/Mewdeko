using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;

namespace Mewdeko.Common.Yml
{
    /// <summary>
    /// Object graph visitor that handles comments associated with objects during YAML serialization.
    /// </summary>
    public class CommentsObjectGraphVisitor : ChainedObjectGraphVisitor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommentsObjectGraphVisitor"/> class.
        /// </summary>
        /// <param name="nextVisitor">The next visitor in the chain.</param>
        public CommentsObjectGraphVisitor(IObjectGraphVisitor<IEmitter> nextVisitor)
            : base(nextVisitor)
        {
        }

        /// <inheritdoc/>
        public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context)
        {
            if (value is CommentsObjectDescriptor commentsDescriptor &&
                !string.IsNullOrWhiteSpace(commentsDescriptor.Comment))
            {
                // Emit a comment event for the associated comment.
                context.Emit(new Comment(commentsDescriptor.Comment.Replace("\n", "\n# "), false));
            }

            return base.EnterMapping(key, value, context);
        }
    }
}