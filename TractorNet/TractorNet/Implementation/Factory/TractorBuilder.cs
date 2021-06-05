using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TractorNet.Implementation.Actor;
using TractorNet.Implementation.Address;
using TractorNet.Implementation.Message;
using TractorNet.Implementation.Pool;

namespace TractorNet.Implementation.Factory
{
    internal sealed class TractorBuilder : ITractorBuilder
    {
        private readonly IServiceCollection services;

        private int? runningNumberLimit;
        private TimeSpan? launchTrottleTime;

        public TractorBuilder(IServiceCollection services)
        {
            this.services = services;
        }

        public void UseLaunchTrottleTime(TimeSpan time)
        {
            launchTrottleTime = time;
        }

        public void UseRunningNumberLimit(int limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            runningNumberLimit = limit;
        }

        public void AddDecorator<T>() where T : class, IActorDecorator
        {
            services.TryAddScoped<T>();

            services.AddScoped<IActorDecorator>(provider => provider.GetRequiredService<T>());
        }

        public void AddDecorator(Func<IServiceProvider, IActorDecorator> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            services.AddScoped(factory);
        }

        public void AddDecorator(Func<IActor, ReceivedMessageContext, CancellationToken, ValueTask> strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            services.AddScoped<IActorDecorator>(_ => new StrategyActorDecorator(strategy));
        }

        public void RegisterActor<T>(Action<IActorBuilder> configuration = null) where T : class, IActor
        {
            services.TryAddScoped<T>();

            CreateActor(provider => provider.GetRequiredService<T>(), configuration);
        }

        public void RegisterActor(Func<IServiceProvider, IActor> factory, Action<IActorBuilder> configuration = null)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            CreateActor(factory, configuration);
        }

        public void RegisterActor(Func<ReceivedMessageContext, CancellationToken, ValueTask> strategy, Action<IActorBuilder> configuration = null)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            CreateActor(_ => new StrategyActor(strategy), configuration);
        }

        public void Build()
        {
            services.AddHostedService<MessageProcessor>();
            services.TryAddSingleton<IAddressBook, MemoryAddressBook>(); //services.AddSingleton in concrete realizations
            services.AddSingleton<MemoryMailbox>();
            services.TryAddSingleton<IInbox>(provider => provider.GetRequiredService<MemoryMailbox>());
            services.TryAddSingleton<IAnonymousOutbox>(provider => provider.GetRequiredService<MemoryMailbox>());
            services.AddSingleton<IAddressPolicy, MatchAllAddressesPolicy>();
            services.AddSingleton<IActorExecutorFactory, ActorExecutorFactory>();

            services.AddSingleton(provider =>
            {
                IActorPool result = null;

                if (runningNumberLimit.HasValue)
                {
                    result = new BlockingBoundedActorPool(runningNumberLimit.Value);
                }
                else
                {
                    result = new UnboundedActorPool();
                }

                if (launchTrottleTime.HasValue)
                {
                    result = new BlockingTrottleActorPoolDecorator(result, launchTrottleTime.Value);
                }

                result = new CompletionActorPoolDecorator(result);

                return result;
            });
        }

        private void CreateActor(Func<IServiceProvider, IActor> factory, Action<IActorBuilder> configuration)
        {
            GetType()
                .GetMethod(nameof(BuildActor), BindingFlags.NonPublic | BindingFlags.Instance)
                .MakeGenericMethod(ActorTypeKeyCreator.Create())
                .Invoke(this, new object[] { factory, configuration });
        }

        private void BuildActor<TActorKey>(Func<IServiceProvider, IActor> factory, Action<IActorBuilder> configuration)
        {
            var actorBuilder = new ActorBuilder<TActorKey>(services, factory);

            configuration?.Invoke(actorBuilder);

            actorBuilder.Build();
        }
    }
}
