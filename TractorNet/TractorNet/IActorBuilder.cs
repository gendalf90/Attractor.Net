using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IActorBuilder
    {
        void UseRunningNumberLimit(int limit);

        void UseLaunchTrottleTime(TimeSpan time);

        void UseMessageProcessingTimeout(TimeSpan time);

        void UseBatching(Action<IBatchBuilder> configuration = null);

        void UseDecorator<T>() where T : class, IActorDecorator;

        void UseDecorator(Func<IServiceProvider, IActorDecorator> factory);

        void UseDecorator(Func<IActor, ReceivedMessageContext, CancellationToken, ValueTask> strategy);

        void UseAddressPolicy<T>() where T : class, IAddressPolicy;

        void UseAddressPolicy(Func<IServiceProvider, IAddressPolicy> factory);

        void UseAddressPolicy(Func<IAddress, CancellationToken, ValueTask<bool>> strategy);
    }
}
