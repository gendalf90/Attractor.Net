using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TractorNet.Implementation.Common;

namespace TractorNet.Implementation.Executor
{
    internal sealed class BatchReceivingActorExecutor : IActorExecutor
    {
        private readonly ConcurrentDictionary<IAddress, Channel<IProcessingMessage>> actorChannels = new ConcurrentDictionary<IAddress, Channel<IProcessingMessage>>(new AddressEqualityComparer());

        private readonly IActorFactory actorFactory;
        private readonly IAddressBook addressBook;
        private readonly IOptions<BatchReceivingSettings> options;

        public BatchReceivingActorExecutor(
            IActorFactory actorFactory,
            IAddressBook addressBook,
            IOptions<BatchReceivingSettings> options)
        {
            this.actorFactory = actorFactory;
            this.addressBook = addressBook;
            this.options = options;
        }

        public async ValueTask ExecuteAsync(IProcessingMessage message, CancellationToken token = default)
        {
            var hasChannel = actorChannels.TryGetValue(message, out var channel);

            if (hasChannel && channel.Writer.TryWrite(message))
            {
                return;
            }

            if (hasChannel || !await TryStartActorAsync(message, token))
            {
                await message.DisposeAsync();
            }
        }

        private async ValueTask<bool> TryStartActorAsync(IProcessingMessage message, CancellationToken token)
        {
            var isActorStarted = false;
            var disposable = new CompositeDisposable();

            try
            {
                var actorPool = actorFactory.CreatePool();

                if (await actorPool.TryUsePlaceAsync(token) is not TrueResult<IAsyncDisposable> usePoolResult)
                {
                    return false;
                }

                disposable.AddFirst(usePoolResult.Value);

                if (await addressBook.TryUseAddressAsync(message, token) is not TrueResult<IAsyncDisposable> useAddressResult)
                {
                    return false;
                }

                disposable.AddFirst(useAddressResult.Value);

                var actorCreator = actorFactory.UseCreator();
                var channel = Channel.CreateBounded<IProcessingMessage>(options.Value.MessageBufferSize ?? 1);
                var feature = new BatchFeature(options);

                channel.Writer.TryWrite(message);
                actorChannels.TryAdd(message, channel);
                disposable.AddFirst(new StrategyDisposable(async () =>
                {
                    channel.Writer.Complete();

                    while (channel.Reader.TryRead(out var message))
                    {
                        await message.DisposeAsync();
                    }
                }));
                disposable.AddFirst(actorCreator);
                disposable.AddLast(new StrategyDisposable(() =>
                {
                    actorChannels.TryRemove(message, out _);
                }));

                _ = Task.Run(async () =>
                {
                    await using (disposable)
                    await using (WithTtl(token, out var ttlToken))
                    {
                        var actor = actorCreator.Create();

                        do
                        {
                            await using (WithTimeout(ttlToken, out var timeoutToken))
                            await using (var message = await channel.Reader.ReadAsync(timeoutToken))
                            {
                                message.SetFeature<IBatchFeature>(feature);

                                var context = new ReceivedMessageContext
                                {
                                    Metadata = message
                                };

                                await Task.Run(async () => await actor.OnReceiveAsync(context, ttlToken)).ContinueWith(_ => feature.AfterRun());
                            }
                        }
                        while (!feature.IsStopped());
                    }
                });

                isActorStarted = true;

                return true;
            }
            finally
            {
                if (!isActorStarted)
                {
                    await disposable.DisposeAsync();
                }
            }
        }

        private IAsyncDisposable WithTtl(CancellationToken token, out CancellationToken result)
        {
            result = token;

            if (!options.Value.ExecutionTimeout.HasValue)
            {
                return new EmptyDisposable();
            }

            return token.WithDelay(options.Value.ExecutionTimeout.Value, out result);
        }

        private IAsyncDisposable WithTimeout(CancellationToken token, out CancellationToken result)
        {
            result = token;

            if (!options.Value.MessageReceivingTimeout.HasValue)
            {
                return new EmptyDisposable();
            }

            return token.WithDelay(options.Value.MessageReceivingTimeout.Value, out result);
        }
    }
}
