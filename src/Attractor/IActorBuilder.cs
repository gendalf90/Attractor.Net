using System;

namespace Attractor
{
    public interface IActorBuilder
    {
        void RegisterActor<T>(Func<T> factory) where T : class, IActor;
        
        void DecorateActor<T>(Func<T> factory) where T : class, IActorDecorator;

        void RegisterCollector<T>(Func<T> factory) where T : class, ICollector;

        void DecorateCollector<T>(Func<T> factory) where T : class, ICollectorDecorator;
    }
}