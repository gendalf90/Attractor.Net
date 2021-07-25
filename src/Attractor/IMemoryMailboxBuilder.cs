using System;

namespace Attractor
{
    public interface IMemoryMailboxBuilder
    {
        void UseReadTrottleTime(TimeSpan time);
    }
}
