using System;

namespace Attractor
{
    public sealed class SendingMetadata
    {
        public TimeSpan? Delay { get; set; }

        public TimeSpan? Ttl { get; set; }
    }
}
