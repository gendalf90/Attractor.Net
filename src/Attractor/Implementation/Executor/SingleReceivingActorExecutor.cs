using System;
using System.Threading;
using System.Threading.Tasks;
using Attractor.Implementation.Common;

namespace Attractor.Implementation.Executor
{
    internal sealed class SingleReceivingActorExecutor : IActorExecutor
    {
        private readonly IAddressBook addressBook;
        private readonly IActorFactory actorFactory;
        private readonly IStateStorage stateStorage;

        public SingleReceivingActorExecutor(
            IActorFactory actorFactory,
            IAddressBook addressBook,
            IStateStorage stateStorage)
        {
            this.addressBook = addressBook;
            this.actorFactory = actorFactory;
            this.stateStorage = stateStorage;
        }

        public async ValueTask<bool> TryExecuteAsync(IProcessingMessage message, CancellationToken token = default)
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
                compositeDisposing.AddLast(message);

                _ = Task.Run(async () =>
                {
                    await using (compositeDisposing)
                    {
                        await stateStorage.LoadStateAsync(message, token);
                        await actorCreator
                            .Create()
                            .OnReceiveAsync(new ReceivedMessageContext
                            {
                                Metadata = message
                            }, token);
                    }
                });

                conditionDisposing.Disable();

                return true;
            }
        }
    }
}
