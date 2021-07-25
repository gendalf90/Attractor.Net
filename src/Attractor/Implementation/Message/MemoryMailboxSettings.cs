using System;

namespace Attractor.Implementation.Message
{
    internal sealed class MemoryMailboxSettings
    {
        public TimeSpan? ReadTrottleTime { get; set; }
    }
}
