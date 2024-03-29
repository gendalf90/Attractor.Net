﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IAddressBook
    {
        ValueTask<TryResult<IAsyncDisposable>> TryUseAddressAsync(IProcessingMessage message, CancellationToken token = default);
    }
}
