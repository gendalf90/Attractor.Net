using System;

namespace Attractor
{
    public interface IBatchBuilder
    {
        void UseExecutionTimeout(TimeSpan time);

        void UseRunCountLimit(int limit);

        void UseBufferSize(int size);

        void UseMessageReceivingTimeout(TimeSpan time);
    }
}
