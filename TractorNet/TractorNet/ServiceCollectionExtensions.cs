using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TractorNet.Implementation.Actor;
using TractorNet.Implementation.Factory;

namespace TractorNet
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTractorServer(this IServiceCollection services, Action<ITractorBuilder> configuration = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var builder = new TractorBuilder(services);

            configuration?.Invoke(builder);

            builder.Build();

            return services;
        }

        public static IServiceCollection ConfigureMemoryMailbox(this IServiceCollection services, Action<IMemoryMailboxBuilder> configuration)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var builder = new MemoryMailboxBuilder(services);

            configuration(builder);

            return services;
        }

        public static void UseDecorator<T>(this IServiceCollection services) where T : class, IActorDecorator
        {
            services.TryAddScoped<T>();

            services.AddScoped<IActorDecorator>(provider => provider.GetRequiredService<T>());
        }

        public static void UseDecorator(this IServiceCollection services, Func<IServiceProvider, IActorDecorator> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            services.AddScoped(factory);
        }

        public static void UseDecorator(this IServiceCollection services, Func<IActor, ReceivedMessageContext, CancellationToken, ValueTask> strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            services.AddScoped<IActorDecorator>(_ => new StrategyActorDecorator(strategy));
        }

        public static void UseDecorator(this IServiceCollection services, Action<IActor, ReceivedMessageContext, CancellationToken> strategy)
        {
            services.UseDecorator((actor, context, token) =>
            {
                strategy(actor, context, token);

                return ValueTask.CompletedTask;
            });
        }

        public static void RegisterActor<T>(this IServiceCollection services, Action<IActorBuilder> configuration = null) where T : class, IActor
        {
            services.TryAddScoped<T>();

            CreateActor(services, provider => provider.GetRequiredService<T>(), configuration);
        }

        public static void RegisterActor(this IServiceCollection services, Func<IServiceProvider, IActor> factory, Action<IActorBuilder> configuration = null)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            CreateActor(services, factory, configuration);
        }

        public static void RegisterActor(this IServiceCollection services, Func<ReceivedMessageContext, CancellationToken, ValueTask> strategy, Action<IActorBuilder> configuration = null)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            CreateActor(services, _ => new StrategyActor(strategy), configuration);
        }

        public static void RegisterActor(this IServiceCollection services, Action<ReceivedMessageContext, CancellationToken> strategy, Action<IActorBuilder> configuration = null)
        {
            services.RegisterActor((context, token) =>
            {
                strategy(context, token);

                return ValueTask.CompletedTask;
            }, configuration);
        }

        private static void CreateActor(IServiceCollection services, Func<IServiceProvider, IActor> factory, Action<IActorBuilder> configuration)
        {
            typeof(ServiceCollectionExtensions)
                .GetMethod(nameof(BuildActor), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(ActorTypeKeyCreator.Create())
                .Invoke(null, new object[] { services, factory, configuration });
        }

        private static void BuildActor<TActorKey>(IServiceCollection services, Func<IServiceProvider, IActor> factory, Action<IActorBuilder> configuration)
        {
            var actorBuilder = new ActorBuilder<TActorKey>(services, factory);

            configuration?.Invoke(actorBuilder);

            actorBuilder.Build();
        }
    }
}
