using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TractorNet.Implementation.Actor;
using TractorNet.Implementation.Address;
using TractorNet.Implementation.Common;
using TractorNet.Implementation.Executor;
using TractorNet.Implementation.Pool;

namespace TractorNet.Implementation.Factory
{
    internal sealed class ActorBuilder<TActorKey> : IActorBuilder, IBatchBuilder
    {
        private readonly IServiceCollection services;
        private readonly Func<IServiceProvider, IActor> actorCreator;

        private int? runningNumberLimit;
        private TimeSpan? launchTrottleTime;
        private TimeSpan? messageProcessingTimeout;
        private Func<IServiceProvider, IAddressPolicy> addressPolicyCreator;
        private BatchReceivingSettings batchReceivingSettings;

        public ActorBuilder(IServiceCollection services, Func<IServiceProvider, IActor> actorCreator)
        {
            this.services = services;
            this.actorCreator = actorCreator;
        }

        public void UseLaunchTrottleTime(TimeSpan time)
        {
            launchTrottleTime = time;
        }

        public void UseMessageProcessingTimeout(TimeSpan time)
        {
            messageProcessingTimeout = time;
        }

        public void UseRunningNumberLimit(int limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            runningNumberLimit = limit;
        }

        public void UseDecorator<T>() where T : class, IActorDecorator
        {
            services.TryAddScoped<T>();

            services.AddScoped(provider => new ActorDecoratorTypedWrapper(provider.GetRequiredService<T>()));
        }

        public void UseDecorator(Func<IServiceProvider, IActorDecorator> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            services.AddScoped(provider => new ActorDecoratorTypedWrapper(factory(provider)));
        }

        public void UseDecorator(Func<IActor, ReceivedMessageContext, CancellationToken, ValueTask> strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            services.AddScoped(_ => new ActorDecoratorTypedWrapper(new StrategyActorDecorator(strategy)));
        }

        public void UseAddressPolicy<T>() where T : class, IAddressPolicy
        {
            services.TryAddSingleton<T>();

            addressPolicyCreator = provider =>
            {
                return provider.GetRequiredService<T>();
            };
        }

        public void UseAddressPolicy(Func<IServiceProvider, IAddressPolicy> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            addressPolicyCreator = factory;
        }

        public void UseAddressPolicy(Func<IAddress, CancellationToken, ValueTask<bool>> strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            addressPolicyCreator = _ => new StrategyAddressPolicy(strategy);
        }

        public void UseBatching(Action<IBatchBuilder> configuration = null)
        {
            batchReceivingSettings = new BatchReceivingSettings();

            configuration?.Invoke(this);
        }

        public void UseExecutionTimeout(TimeSpan time)
        {
            batchReceivingSettings.ExecutionTimeout = time;
        }

        public void UseRunCountLimit(int limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            batchReceivingSettings.RunCountLimit = limit;
        }

        public void UseMessageReceivingTimeout(TimeSpan time)
        {
            batchReceivingSettings.MessageReceivingTimeout = time;
        }

        public void UseBufferSize(int size)
        {
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            batchReceivingSettings.MessageBufferSize = size;
        }

        public void Build()
        {
            services.AddSingleton<ActorFactory>();
            services.AddSingleton<IActorFactory>(provider => provider.GetRequiredService<ActorFactory>());
            services.AddScoped(provider => new ActorTypedWrapper(actorCreator(provider)));
            services.AddScoped(provider =>
            {
                IActor result = provider.GetRequiredService<ActorTypedWrapper>();

                var currentDecorators = provider.GetServices<ActorDecoratorTypedWrapper>();
                var commonDecorators = provider.GetServices<IActorDecorator>();

                foreach (var decorator in currentDecorators.Reverse())
                {
                    result = decorator.Wrap(result);
                }

                foreach (var decorator in commonDecorators.Reverse())
                {
                    result = decorator.Wrap(result);
                }

                if (messageProcessingTimeout.HasValue)
                {
                    result = new ProcessingTimeoutActorDecorator(messageProcessingTimeout.Value).Wrap(result);
                }

                return new ResultActorTypedWrapper(result);
            });
            services.AddSingleton(provider =>
            {
                IActorExecutor result = null;

                if (batchReceivingSettings != null)
                {
                    result = new BatchReceivingActorExecutor(
                        provider.GetRequiredService<ActorFactory>(),
                        provider.GetRequiredService<IAddressBook>(),
                        Options.Create(batchReceivingSettings));
                }
                else
                {
                    result = new SingleReceivingActorExecutor(
                        provider.GetRequiredService<ActorFactory>(),
                        provider.GetRequiredService<IAddressBook>());
                }

                return new ActorExecutorTypedWrapper(result);
            });
            services.AddSingleton(provider =>
            {
                var result = addressPolicyCreator == null
                    ? provider.GetRequiredService<IAddressPolicy>()
                    : addressPolicyCreator(provider);

                return new AddressPolicyTypedWrapper(result);
            });
            services.AddSingleton(provider =>
            {
                var result = provider.GetRequiredService<IActorPool>();

                if (runningNumberLimit.HasValue)
                {
                    result = new NonBlockingBoundedActorPoolDecorator(result, runningNumberLimit.Value);
                }

                if (launchTrottleTime.HasValue)
                {
                    result = new NonBlockingTrottleActorPoolDecorator(result, launchTrottleTime.Value);
                }

                return new ActorPoolTypedWrapper(result);
            });
        }

        private class ActorFactory : IActorFactory
        {
            private readonly IServiceProvider provider;

            public ActorFactory(IServiceProvider provider)
            {
                this.provider = provider;
            }

            public IAddressPolicy CreateAddressPolicy()
            {
                return provider.GetRequiredService<AddressPolicyTypedWrapper>();
            }

            public IActorExecutor CreateExecutor()
            {
                return provider.GetRequiredService<ActorExecutorTypedWrapper>();
            }

            public IActorPool CreatePool()
            {
                return provider.GetRequiredService<ActorPoolTypedWrapper>();
            }

            public IActorCreator UseCreator()
            {
                return new ActorCreator(provider.CreateScope());
            }
        }

        private class ActorCreator : IActorCreator
        {
            private readonly IServiceScope scope;

            public ActorCreator(IServiceScope scope)
            {
                this.scope = scope;
            }

            public IActor Create()
            {
                return scope.ServiceProvider.GetRequiredService<ResultActorTypedWrapper>();
            }

            public ValueTask DisposeAsync()
            {
                scope.Dispose();

                return ValueTask.CompletedTask;
            }
        }

        private class ResultActorTypedWrapper : IActor
        {
            private readonly IActor actor;

            public ResultActorTypedWrapper(IActor actor)
            {
                this.actor = actor;
            }

            public ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
            {
                return actor.OnReceiveAsync(context, token);
            }
        }

        private class ActorTypedWrapper : IActor, IDisposable
        {
            private readonly IActor actor;

            public ActorTypedWrapper(IActor actor)
            {
                this.actor = actor;
            }

            public ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
            {
                return actor.OnReceiveAsync(context, token);
            }

            public void Dispose()
            {
                actor.TryDispose();
            }
        }

        private class ActorDecoratorTypedWrapper : IActorDecorator, IDisposable
        {
            private readonly IActorDecorator actorDecorator;

            public ActorDecoratorTypedWrapper(IActorDecorator actorDecorator)
            {
                this.actorDecorator = actorDecorator;
            }

            public void Decorate(IActor actor)
            {
                actorDecorator.Decorate(actor);
            }

            public ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
            {
                return actorDecorator.OnReceiveAsync(context, token);
            }

            public void Dispose()
            {
                actorDecorator.TryDispose();
            }
        }

        private class ActorExecutorTypedWrapper : IActorExecutor
        {
            private readonly IActorExecutor actorExecutor;

            public ActorExecutorTypedWrapper(IActorExecutor actorExecutor)
            {
                this.actorExecutor = actorExecutor;
            }

            public ValueTask<bool> TryExecuteAsync(IProcessingMessage message, CancellationToken token = default)
            {
                return actorExecutor.TryExecuteAsync(message, token);
            }
        }

        private class AddressPolicyTypedWrapper : IAddressPolicy
        {
            private readonly IAddressPolicy addressPolicy;

            public AddressPolicyTypedWrapper(IAddressPolicy addressPolicy)
            {
                this.addressPolicy = addressPolicy;
            }

            public ValueTask<bool> IsMatchAsync(IAddress address, CancellationToken token = default)
            {
                return addressPolicy.IsMatchAsync(address, token);
            }
        }

        private class ActorPoolTypedWrapper : IActorPool
        {
            private readonly IActorPool actorPool;

            public ActorPoolTypedWrapper(IActorPool actorPool)
            {
                this.actorPool = actorPool;
            }

            public ValueTask<TryResult<IAsyncDisposable>> TryUsePlaceAsync(CancellationToken token = default)
            {
                return actorPool.TryUsePlaceAsync(token);
            }
        }
    }
}
