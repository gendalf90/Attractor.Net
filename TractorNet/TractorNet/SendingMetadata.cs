using System;

namespace TractorNet
{
    public sealed class SendingMetadata
    {
        public TimeSpan? Delay { get; set; }

        public TimeSpan? Ttl { get; set; }
    }
}
