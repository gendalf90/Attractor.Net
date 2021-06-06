using System;

namespace TractorNet
{
    public interface ITractorBuilder
    {
        void UseRunningNumberLimit(int limit);

        void UseLaunchTrottleTime(TimeSpan time);
    }
}
