using System;

namespace TractorNet.Implementation.Executor
{
    internal sealed class BatchReceivingSettings
    {
        public int? MessageBufferSize { get; set; }

        public TimeSpan? ExecutionTimeout { get; set; }

        public int? MessageProcessedLimit { get; set; }

        public TimeSpan? MessageReceivingTimeout { get; set; }
    }
}
