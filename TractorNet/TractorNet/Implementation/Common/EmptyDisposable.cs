using System;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Common
{
    internal sealed class EmptyDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
