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
        private static readonly int DefaultBufferSize = 1;

        private readonly ConcurrentDictionary<IAddress, Channel<IProcessingMessage>> actorChannels = new ConcurrentDictionary<IAddress, Channel<IProcessingMessage>>(new AddressEqualityComparer());

        private readonly IActorFactory actorFactory;
        private readonly IAddressBook addressBook;
        private readonly IStateStorage stateStorage;
        private readonly IOptions<BatchReceivingSettings> options;

        public BatchReceivingActorExecutor(
            IActorFactory actorFactory,
            IAddressBook addressBook,
            IStateStorage stateStorage,
            IOptions<BatchReceivingSettings> options)
        {
            this.actorFactory = actorFactory;
            this.addressBook = addressBook;
            this.stateStorage = stateStorage;
            this.options = options;
        }

        public async ValueTask<bool> TryExecuteAsync(IProcessingMessage message, CancellationToken token = default)
        {
            if (actorChannels.TryGetValue(message, out var channel))
            {
                return channel.Writer.TryWrite(message);
            }

            return await TryStartActorAsync(message, token);
        }

        private async ValueTask<bool> TryStartActorAsync(IProcessingMessage message, CancellationToken token)
        {
            var compositeDisposing = new CompositeDisposable();

            await using (var conditionDisposing = new ConditionDisposable(compositeDisposing, true))
            {
                var actorPool = actorFactory.CreatePool();

                if (await actorPool.TryUsePlaceAsync(token) is not TrueResult<IAsyncDisposable> usePoolResult)
                {
                    return false;
                }

                compositeDisposing.AddLast(usePoolResult.Value);

                if (await addressBook.TryUseAddressAsync(message, token) is not TrueResult<IAsyncDisposable> useAddressResult)
                {
                    return false;
                }

                compositeDisposing.AddLast(useAddressResult.Value);

                var actorCreator = actorFactory.UseCreator();

                compositeDisposing.AddFirst(actorCreator);

                var channel = Channel.CreateBounded<IProcessingMessage>(options.Value.MessageBufferSize ?? DefaultBufferSize);
                
                channel.Writer.TryWrite(message);
                actorChannels.TryAdd(message, channel);
                compositeDisposing.AddLast(new StrategyDisposable(async () =>
                {
                    channel.Writer.Complete();

                    while (channel.Reader.TryRead(out var message))
                    {
                        await message.DisposeAsync();
                    }

                    actorChannels.TryRemove(message, out _);
                }));

                var feature = new BatchFeature(options);

                _ = Task.Run(async () =>
                {
                    await using (compositeDisposing)
                    await using (WithTtl(token, out var ttlToken))
                    {
                        var actor = actorCreator.Create();

                        do
                        {
                            await using (WithTimeout(ttlToken, out var timeoutToken))
                            await using (var message = await channel.Reader.ReadAsync(timeoutToken))
                            {
                                await stateStorage.LoadStateAsync(message, ttlToken);

                                message.SetFeature<IBatchFeature>(feature);

                                var context = new ReceivedMessageContext
                                {
                                    Metadata = message
                                };

                                await actor.OnReceiveAsync(context, ttlToken);

                                feature.AfterRun();
                            }
                        }
                        while (!feature.IsStopped());
                    }
                });

                conditionDisposing.Disable();

                return true;
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
