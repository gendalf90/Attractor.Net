using System.Threading.Tasks;

namespace Attractor;

public interface IRequestAwaiter
{
    Task Completion { get; }
}