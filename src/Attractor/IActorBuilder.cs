using System;

namespace Attractor;

public interface IActorBuilder
{
    void Decorate<T>(Func<T> factory) where T : class, IActor, IDecorator<IActor>;
}