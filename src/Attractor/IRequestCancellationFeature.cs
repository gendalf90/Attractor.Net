using System.Threading;

namespace Attractor;

public interface IRequestCancellationFeature
{
    CancellationToken Token { get; }
}