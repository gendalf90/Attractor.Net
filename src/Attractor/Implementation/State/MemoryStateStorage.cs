using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Attractor.Implementation.Common;

namespace Attractor.Implementation.State
{
    internal sealed class MemoryStateStorage : IStateStorage
    {
        private readonly ConcurrentDictionary<IAddress, IState> states = new ConcurrentDictionary<IAddress, IState>(new AddressEqualityComparer());

        public ValueTask LoadStateAsync(IProcessingMessage message, CancellationToken token = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            token.ThrowIfCancellationRequested();

            message.SetFeature<IStateFeature>(new StateFeature(message, this));

            return ValueTask.CompletedTask;
        }

        private class StateFeature : IStateFeature
        {
            private readonly IAddress address;
            private readonly MemoryStateStorage storage;

            public StateFeature(IAddress address, MemoryStateStorage storage)
            {
                this.address = address;
                this.storage = storage;
            }

            public ReadOnlyMemory<byte> GetBytes()
            {
                if (storage.states.TryGetValue(address, out var state))
                {
                    return state.GetBytes();
                }

                return ReadOnlyMemory<byte>.Empty;
            }

            public ValueTask SaveAsync(IState state, CancellationToken token = default)
            {
                if (state == null)
                {
                    throw new ArgumentNullException(nameof(state));
                }

                storage.states.AddOrUpdate(address, state, (_, _) => state);

                return ValueTask.CompletedTask;
            }

            public ValueTask ClearAsync(CancellationToken token = default)
            {
                storage.states.TryRemove(address, out _);

                return ValueTask.CompletedTask;
            }
        }
    }
}
