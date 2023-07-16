using System;
using Microsoft.Extensions.DependencyInjection;

namespace Attractor
{
    public interface IServicesActorBuilder : IActorBuilder
    {
        IServiceCollection Services { get; }

        void RegisterActor<T>(Func<IServiceProvider, T> factory) where T : class, IActor;
        
        void DecorateActor<T>(Func<IServiceProvider, T> factory) where T : class, IActorDecorator;

        void RegisterCollector<T>(Func<IServiceProvider, T> factory) where T : class, ICollector;

        void DecorateCollector<T>(Func<IServiceProvider, T> factory) where T : class, ICollectorDecorator;
    }
}