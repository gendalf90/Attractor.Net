using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Common
{
    internal sealed class CompositeDisposable : IAsyncDisposable
    {
        private readonly LinkedList<IAsyncDisposable> disposables;

        public CompositeDisposable(params IAsyncDisposable[] disposables)
        {
            this.disposables = new LinkedList<IAsyncDisposable>(disposables);
        }

        public void AddFirst(IAsyncDisposable disposable)
        {
            disposables.AddFirst(disposable);
        }

        public void AddLast(IAsyncDisposable disposable)
        {
            disposables.AddLast(disposable);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var disposable in disposables)
            {
                await disposable.DisposeAsync();
            }
        }
    }
}
