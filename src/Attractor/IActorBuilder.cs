using System;

namespace Attractor
{
    public interface IActorBuilder
    {
        void RegisterActor<T>(Func<T> factory) where T : class, IActor;
        
        void DecorateActor<T>(Func<T> factory) where T : class, IActorDecorator;
    }
}