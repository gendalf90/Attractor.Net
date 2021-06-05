using System;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Common
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
    }
}
