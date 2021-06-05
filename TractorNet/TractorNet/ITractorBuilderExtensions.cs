using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public static class ITractorBuilderExtensions
    {
        public static void AddDecorator(this ITractorBuilder builder, Action<IActor, ReceivedMessageContext, CancellationToken> strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            builder.AddDecorator((actor, context, token) =>
            {
                strategy(actor, context, token);

                return ValueTask.CompletedTask;
            });
        }

        public static void RegisterActor(
            this ITractorBuilder builder, 
            Action<ReceivedMessageContext, CancellationToken> strategy, 
            Action<IActorBuilder> configuration = null)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            builder.RegisterActor((context, token) =>
            {
                strategy(context, token);

                return ValueTask.CompletedTask;
            }, configuration);
        }
    }
}
