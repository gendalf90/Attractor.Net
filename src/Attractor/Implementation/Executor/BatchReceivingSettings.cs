using System;

namespace Attractor.Implementation.Executor
{
    internal sealed class BatchReceivingSettings
    {
        public int? MessageBufferSize { get; set; }

        public TimeSpan? ExecutionTimeout { get; set; }

        public int? RunCountLimit { get; set; }

        public TimeSpan? MessageReceivingTimeout { get; set; }
    }
}
