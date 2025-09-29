using System.Threading;

namespace Attractor.Implementation;

internal class RequestCancellation(CancellationToken token) : IRequestCancellation
{
    public CancellationToken Token => token;
}