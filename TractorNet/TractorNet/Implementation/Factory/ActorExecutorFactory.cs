using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TractorNet.Implementation.Factory
{
    internal sealed class ActorExecutorFactory : IActorExecutorFactory
    {
        private readonly IServiceProvider services;

        public ActorExecutorFactory(IServiceProvider services)
        {
            this.services = services;
        }

        public async ValueTask<TryResult<IActorExecutor>> TryCreateByAddressAsync(IAddress address, CancellationToken token = default)
        {
            foreach (var actorFactory in services.GetServices<IActorFactory>())
            {
                var addressPolicy = actorFactory.CreateAddressPolicy();

                if (await addressPolicy.IsMatchAsync(address, token))
                {
                    return new TrueResult<IActorExecutor>(actorFactory.CreateExecutor());
                }
            }

            return new FalseResult<IActorExecutor>();
        }
    }
}
