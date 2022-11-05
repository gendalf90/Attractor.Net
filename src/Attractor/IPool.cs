using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    internal interface IPool
    {
        ValueTask<IAsyncDisposable> AddAsync(CancellationToken token = default);
    }
}
