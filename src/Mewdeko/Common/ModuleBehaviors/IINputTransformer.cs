using System.Threading.Tasks;

namespace Mewdeko.Common.ModuleBehaviors;

public interface IInputTransformer
{
    Task<string> TransformInput(IGuild guild, IMessageChannel channel, IUser user, string input);
}