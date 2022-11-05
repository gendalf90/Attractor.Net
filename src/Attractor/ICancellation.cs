using System.Threading;

namespace Attractor
{
    public interface ICancellation
    {
        void Cancel();

        CancellationToken GetToken();
    }
}
