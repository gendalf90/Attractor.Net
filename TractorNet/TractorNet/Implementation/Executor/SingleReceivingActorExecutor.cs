using System;
using System.Threading;
using System.Threading.Tasks;
using TractorNet.Implementation.Common;

namespace TractorNet.Implementation.Executor
{
    internal sealed class SingleReceivingActorExecutor : IActorExecutor
    {
        private readonly IAddressBook addressBook;
        private readonly IActorFactory actorFactory;

        public SingleReceivingActorExecutor(
            IActorFactory actorFactory,
            IAddressBook addressBook)
        {
            this.addressBook = addressBook;
            this.actorFactory = actorFactory;
        }

        public async ValueTask ExecuteAsync(IProcessingMessage message, CancellationToken token = default)
        {
            var isActorStarted = false;
            var disposable = new CompositeDisposable(message);
            
            try
            {
                var actorPool = actorFactory.CreatePool();

                if (await actorPool.TryUsePlaceAsync(token) is not TrueResult<IAsyncDisposable> usePoolResult)
                {
                    return;
                }

                disposable.AddLast(usePoolResult.Value);

                if (await addressBook.TryUseAddressAsync(message, token) is not TrueResult<IAsyncDisposable> useAddressResult)
                {
                    return;
                }

                disposable.AddLast(useAddressResult.Value);

                var actorCreator = actorFactory.UseCreator();

                disposable.AddFirst(actorCreator);

                _ = Task.Run(async () =>
                {
                    await using (disposable)
                    {
                        await actorCreator
                            .Create()
                            .OnReceiveAsync(new ReceivedMessageContext
                            {
                                Metadata = message
                            }, token);
                    }
                });

                isActorStarted = true;
            }
            finally
            {
                if (!isActorStarted)
                {
                    await disposable.DisposeAsync();
                }
            }
        }
    }
}
