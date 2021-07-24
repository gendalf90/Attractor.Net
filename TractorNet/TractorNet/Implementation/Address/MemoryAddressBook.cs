using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TractorNet.Implementation.Common;

namespace TractorNet.Implementation.Address
{
    internal sealed class MemoryAddressBook : IAddressBook
    {
        private readonly ConcurrentDictionary<IAddress, MemoryAddressReservation> reservations = new ConcurrentDictionary<IAddress, MemoryAddressReservation>(new AddressEqualityComparer());

        public ValueTask<TryResult<IAsyncDisposable>> TryUseAddressAsync(IProcessingMessage message, CancellationToken token = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            token.ThrowIfCancellationRequested();

            var reservation = new MemoryAddressReservation(message, this);

            return reservations.TryAdd(message, reservation) 
                ? ValueTask.FromResult<TryResult<IAsyncDisposable>>(new TrueResult<IAsyncDisposable>(reservation))
                : ValueTask.FromResult<TryResult<IAsyncDisposable>>(new FalseResult<IAsyncDisposable>());
        }

        private class MemoryAddressReservation : IAsyncDisposable
        {
            private readonly IAddress address;
            private readonly MemoryAddressBook book;

            public MemoryAddressReservation(IAddress address, MemoryAddressBook book)
            {
                this.address = address;
                this.book = book;
            }

            public ValueTask DisposeAsync()
            {
                book.reservations.TryRemove(address, out _);

                return ValueTask.CompletedTask;
            }
        }
    }
}
