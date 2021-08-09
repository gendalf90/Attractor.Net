using Attractor.Implementation.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public static class IActorBuilderExtensions
    {
        public static void UseDecorator(this IActorBuilder builder, Action<IActor, ReceivedMessageContext, CancellationToken> strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            builder.UseDecorator((actor, context, token) =>
            {
                strategy(actor, context, token);

                return ValueTaskBuilder.CompletedTask;
            });
        }

        public static void UseAddressPolicy(this IActorBuilder builder, Func<IAddress, CancellationToken, bool> strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            builder.UseAddressPolicy((address, token) =>
            {
                return ValueTaskBuilder.FromResult(strategy(address, token));
            });
        }
    }
}
