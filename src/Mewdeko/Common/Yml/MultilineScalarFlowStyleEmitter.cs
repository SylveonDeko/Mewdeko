using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace Mewdeko.Common.Yml
{
    /// <summary>
    /// Event emitter responsible for emitting multi-line scalar values in a literal style during YAML serialization.
    /// </summary>
    public class MultilineScalarFlowStyleEmitter : ChainedEventEmitter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MultilineScalarFlowStyleEmitter"/> class.
        /// </summary>
        /// <param name="nextEmitter">The next emitter in the chain.</param>
        public MultilineScalarFlowStyleEmitter(IEventEmitter nextEmitter)
            : base(nextEmitter)
        {
        }

        /// <inheritdoc/>
        public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
        {
            if (typeof(string).IsAssignableFrom(eventInfo.Source.Type))
            {
                var value = eventInfo.Source.Value as string;
                if (!string.IsNullOrEmpty(value))
                {
                    // Determine if the string value contains any multi-line characters.
                    var isMultiLine = value.IndexOfAny([
                        '\r', '\n', '\x85', '\x2028', '\x2029'
                    ]) >= 0;
                    if (isMultiLine)
                    {
                        // Emit the scalar event in literal style for multi-line values.
                        eventInfo = new ScalarEventInfo(eventInfo.Source)
                        {
                            Style = ScalarStyle.Literal
                        };
                    }
                }
            }

            // Continue emitting the event down the chain.
            nextEmitter.Emit(eventInfo, emitter);
        }
    }
}