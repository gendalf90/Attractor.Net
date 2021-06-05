using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface ITractorBuilder
    {
        void UseRunningNumberLimit(int limit);

        void UseLaunchTrottleTime(TimeSpan time);

        void AddDecorator<T>() where T : class, IActorDecorator;

        void AddDecorator(Func<IServiceProvider, IActorDecorator> factory);

        void AddDecorator(Func<IActor, ReceivedMessageContext, CancellationToken, ValueTask> strategy);

        void RegisterActor<T>(Action<IActorBuilder> configuration = null) where T : class, IActor;

        void RegisterActor(Func<IServiceProvider, IActor> factory, Action<IActorBuilder> configuration = null);

        void RegisterActor(Func<ReceivedMessageContext, CancellationToken, ValueTask> strategy, Action<IActorBuilder> configuration = null);
    }
}
