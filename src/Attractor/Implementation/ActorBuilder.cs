using System;

namespace Attractor.Implementation
{
    internal class ActorBuilder : IActorBuilder
    {
        private Func<IActor> defaultActorFactory = Actor.Empty;
        private Func<IActor, IActor> actorDecoratorFactory = _ => _;
        
        void IActorBuilder.Decorate<T>(Func<T> factory)
        {
            ArgumentNullException.ThrowIfNull(factory, nameof(factory));

            actorDecoratorFactory = Decorate(actorDecoratorFactory, factory);
        }

        void IActorBuilder.Register<T>(Func<T> factory)
        {
            defaultActorFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public IActor Build()
        {
            return actorDecoratorFactory(defaultActorFactory());
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
}