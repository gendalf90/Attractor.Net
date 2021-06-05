using Microsoft.Extensions.Options;

namespace TractorNet.Implementation.Executor
{
    internal sealed class BatchFeature : IBatchFeature
    {
        private readonly IOptions<BatchReceivingSettings> options;

        private int processedMessagesCount;
        private bool isStopped;

        public BatchFeature(IOptions<BatchReceivingSettings> options)
        {
            this.options = options;
        }

        public void StopProcessing()
        {
            isStopped = true;
        }

        public void OnMessageProcessed()
        {
            processedMessagesCount++;
        }

        public bool IsStopped()
        {
            return isStopped || processedMessagesCount == options.Value.MessageProcessedLimit;
        }
    }
}
