using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Common
{
    internal sealed class CompositeDisposable : IAsyncDisposable
    {
        private readonly List<IAsyncDisposable> disposables = new List<IAsyncDisposable>();

        public CompositeDisposable(params IAsyncDisposable[] disposables)
        {
            this.disposables.AddRange(disposables);
        }

        public void Add(IAsyncDisposable disposable)
        {
            disposables.Add(disposable);
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
