using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet.Implementation
{
    internal sealed class MessageProcessor : BackgroundService
    {
        private readonly IInbox inbox;
        private readonly IActorExecutorFactory factory;
        private readonly ILogger<MessageProcessor> logger;

        public MessageProcessor(
            IInbox inbox, 
            IActorExecutorFactory factory,
            ILogger<MessageProcessor> logger)
        {
            this.inbox = inbox;
            this.factory = factory;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogTrace("Message processing has started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuteInternalAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Message processing has error");
                }
            }

            logger.LogTrace("Message processing has stopped");
        }

        private async Task ExecuteInternalAsync(CancellationToken stoppingToken)
        {
            await foreach (var message in inbox.ReadMessagesAsync(stoppingToken))
            {
                try
                {
                    if (await factory.TryCreateByAddressAsync(message, stoppingToken) is TrueResult<IActorExecutor> createExecutorResult)
                    {
                        await createExecutorResult.Value.ExecuteAsync(message, stoppingToken);
                    }
                }
                catch
                {
                    await message.DisposeAsync();

                    throw;
                }
            }
        }
    }
}
