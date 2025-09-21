using System;

namespace Attractor.Implementation;

internal class ActorBuilder(IActor actor) : IActorBuilder
{
    private Func<IActor, IActor> actorDecoratorFactory = _ => _;

    void IActorBuilder.Decorate<T>(Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));

        actorDecoratorFactory = Decorate(actorDecoratorFactory, factory);
    }

    public IActor Build()
    {
        return actorDecoratorFactory(actor);
    }

    private static Func<TResult, TResult> Decorate<TResult, TDecorator>(Func<TResult, TResult> resultFactory, Func<TDecorator> decoratorFactory)
        where TDecorator : class, TResult, IDecorator<TResult>
    {
        return decoratee =>
        {
            var result = resultFactory(decoratee);
            var decorator = decoratorFactory();

            decorator.Decorate(result);

            return decorator;
        };
    }
}
