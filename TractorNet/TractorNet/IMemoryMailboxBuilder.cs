using System;

namespace TractorNet
{
    public interface IMemoryMailboxBuilder
    {
        void UseReadTrottleTime(TimeSpan time);
    }
}
