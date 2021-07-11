using System;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Common
{
    internal sealed class ConditionDisposable : IAsyncDisposable
    {
        private readonly IAsyncDisposable disposable;

        private bool isEnabled;

        public ConditionDisposable(IAsyncDisposable disposable, bool isEnabled)
        {
            this.disposable = disposable;
            this.isEnabled = isEnabled;
        }

        public void Enable()
        {
            isEnabled = true;
        }

        public void Disable()
        {
            isEnabled = false;
        }

        public async ValueTask DisposeAsync()
        {
            if (isEnabled)
            {
                await disposable.DisposeAsync();
            }
        }
    }
}
