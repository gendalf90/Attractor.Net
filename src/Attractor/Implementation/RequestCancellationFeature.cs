using System.Threading;

namespace Attractor.Implementation;

internal class RequestCancellationFeature(CancellationToken token) : IRequestCancellationFeature
{
    public CancellationToken Token => token;
}