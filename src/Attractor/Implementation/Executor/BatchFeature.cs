using Microsoft.Extensions.Options;

namespace Attractor.Implementation.Executor
{
    internal sealed class BatchFeature : IBatchFeature
    {
        private readonly IOptions<BatchReceivingSettings> options;

        private int runCount;
        private bool isStopped;

        public BatchFeature(IOptions<BatchReceivingSettings> options)
        {
            this.options = options;
        }

        public void StopProcessing()
        {
            isStopped = true;
        }

        public void AfterRun()
        {
            runCount++;
        }

        public bool IsStopped()
        {
            return isStopped || runCount == options.Value.RunCountLimit;
        }
    }
}
