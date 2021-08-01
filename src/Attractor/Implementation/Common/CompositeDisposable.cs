using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Attractor.Implementation.Common
{
    internal sealed class CompositeDisposable : IAsyncDisposable
    {
        private readonly LinkedList<IAsyncDisposable> disposables;

        public CompositeDisposable()
        {
            disposables = new LinkedList<IAsyncDisposable>();
        }

        public CompositeDisposable(IEnumerable<IAsyncDisposable> disposables)
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

        public static CompositeDisposable Create(params IAsyncDisposable[] disposables)
        {
            return new CompositeDisposable(disposables);
        }
    }
}
