using System;

namespace TractorNet
{
    public interface IMemoryMessagesBuilder
    {
        void UseReadTrottleTime(TimeSpan time);
    }
}
