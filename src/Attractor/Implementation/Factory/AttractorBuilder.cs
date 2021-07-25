using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using Attractor.Implementation.Address;
using Attractor.Implementation.Message;
using Attractor.Implementation.Pool;
using Attractor.Implementation.State;

namespace Attractor.Implementation.Factory
{
    internal sealed class AttractorBuilder : IAttractorBuilder
    {
        private readonly IServiceCollection services;

        private int? runningNumberLimit;
        private TimeSpan? launchTrottleTime;

        public AttractorBuilder(IServiceCollection services)
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

        public void Build()
        {
            services.AddHostedService<MessageProcessor>();
            services.TryAddSingleton<IAddressBook, MemoryAddressBook>();
            services.TryAddSingleton<IStateStorage, MemoryStateStorage>();
            services.AddSingleton<MemoryMailbox>();
            services.TryAddSingleton<IInbox>(provider => provider.GetRequiredService<MemoryMailbox>());
            services.TryAddSingleton<IAnonymousOutbox>(provider => provider.GetRequiredService<MemoryMailbox>());
            services.AddSingleton<IAddressPolicy, RejectAllAddressesPolicy>();
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
    }
}
