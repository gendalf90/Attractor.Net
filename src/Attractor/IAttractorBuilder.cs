using System;

namespace Attractor
{
    public interface IAttractorBuilder
    {
        void UseRunningNumberLimit(int limit);

        void UseLaunchTrottleTime(TimeSpan time);
    }
}
