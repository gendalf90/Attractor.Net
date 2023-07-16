using Attractor.Implementation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Attractor
{
    public static class RegistrationExtensions
    {
        public static IServiceCollection AddActorSystem(this IServiceCollection services, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));

            return services.AddSingleton(provider =>
            {
                var system = ActorSystem.Create(token);

                foreach (var builder in provider.GetServices<ActorBuilder>())
                {
                    builder.Build(provider, system);
                }

                return system;
            });
        }

        public static IServiceCollection AddActor(
            this IServiceCollection services, 
            IAddressPolicy policy, 
            Action<IServicesActorBuilder> configuration = null)
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));
            ArgumentNullException.ThrowIfNull(policy, nameof(policy));

            var builder = new ActorBuilder(services, policy);

            configuration?.Invoke(builder);

            return services.AddSingleton(builder);
        }

        private class ActorBuilder : IServicesActorBuilder
        {
            private readonly IServiceCollection services;
            private readonly IAddressPolicy policy;

            private readonly List<Action<IServiceProvider, IActorBuilder>> buildList = new();

            public ActorBuilder(IServiceCollection services, IAddressPolicy policy)
            {
                this.services = services;
                this.policy = policy;
            }

            IServiceCollection IServicesActorBuilder.Services => services;

            public void Build(IServiceProvider provider, IActorSystem system)
            {
                system.Register(policy, builder =>
                {
                    foreach (var build in buildList)
                    {
                        build(provider, builder);
                    }
                });
            }

            void IServicesActorBuilder.DecorateActor<T>(Func<IServiceProvider, T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));
                
                buildList.Add((provider, builder) => builder.DecorateActor(Partial(factory, provider)));
            }

            void IActorBuilder.DecorateActor<T>(Func<T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));
                
                buildList.Add((_, builder) => builder.DecorateActor(factory));
            }

            void IServicesActorBuilder.RegisterActor<T>(Func<IServiceProvider, T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));
                
                buildList.Add((provider, builder) => builder.RegisterActor(Partial(factory, provider)));
            }

            void IActorBuilder.RegisterActor<T>(Func<T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));
                
                buildList.Add((_, builder) => builder.RegisterActor(factory));
            }

            void IServicesActorBuilder.RegisterCollector<T>(Func<IServiceProvider, T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));

                buildList.Add((provider, builder) => builder.RegisterCollector(Partial(factory, provider)));
            }

            void IServicesActorBuilder.DecorateCollector<T>(Func<IServiceProvider, T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));

                buildList.Add((provider, builder) => builder.DecorateCollector(Partial(factory, provider)));
            }

            void IActorBuilder.RegisterCollector<T>(Func<T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));

                buildList.Add((_, builder) => builder.RegisterCollector(factory));
            }

            void IActorBuilder.DecorateCollector<T>(Func<T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));

                buildList.Add((_, builder) => builder.DecorateCollector(factory));
            }

            private static Func<TOutput> Partial<TInput, TOutput>(Func<TInput, TOutput> func, TInput value)
            {
                return () => func(value);
            }
        }
    }
}
