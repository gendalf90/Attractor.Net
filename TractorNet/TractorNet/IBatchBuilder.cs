using System;

namespace TractorNet
{
    public interface IBatchBuilder
    {
        void UseExecutionTimeout(TimeSpan time);

        void UseProcessedMessagesLimit(int limit);

        void UseBufferSize(int size);

        void UseMessageReceivingTimeout(TimeSpan time);
    }
}
