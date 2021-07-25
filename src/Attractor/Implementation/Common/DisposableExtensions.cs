using System;
using System.Threading.Tasks;

namespace Attractor.Implementation.Common
{
    internal static class DisposableExtensions
    {
        public static async ValueTask TryDisposeAsync(this object obj)
        {
            if (obj is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public static void TryDispose(this object obj)
        {
            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
