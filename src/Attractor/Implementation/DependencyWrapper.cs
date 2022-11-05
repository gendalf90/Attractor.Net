using System;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal class DependencyWrapper<TValue> //: IAsyncDisposable
    {
        private readonly TValue value;

        public DependencyWrapper(TValue value)
        {
            this.value = value;
        }

        public TValue Value { get => value; }

        //public ValueTask DisposeAsync()
        //{
        //    return value.TryDisposeAsync();
        //}
    }
}
