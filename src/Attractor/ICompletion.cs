using System.Threading.Tasks;

namespace Attractor;

public interface ICompletion
{
    Task Completion { get; }
}