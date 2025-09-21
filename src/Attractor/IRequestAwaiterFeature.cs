using System.Threading.Tasks;

namespace Attractor;

public interface IRequestAwaiterFeature
{
    Task Completion { get; }
}