using System;

namespace Attractor
{
    public interface IActorBuilder
    {
        void Register<T>(Func<T> factory) where T : class, IActor;
        
        void Decorate<T>(Func<T> factory) where T : class, IActorDecorator;
    }
}