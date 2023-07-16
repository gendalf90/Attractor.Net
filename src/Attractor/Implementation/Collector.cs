using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Collector
    {
        private static readonly ICollector empty = new Instance(null);

        public static ICollectorDecorator FromStrategy(OnCollectDecorator onCollect = null)
        {
            return new Decorator(onCollect);
        }

        public static ICollector FromStrategy(OnCollect onCollect = null)
        {
            return new Instance(onCollect);
        }

        public static ICollector NewProcess()
        {
            IActorRef process = null;

            return FromStrategy(async (context, token) =>
            {
                var system = context.Get<IActorSystem>();
                var address = context.Get<IAddress>();

                if (system == null || address == null)
                {
                    return;
                }

                process ??= system.Refer(address);

                await process.PostAsync(context, token);
            });
        }

        public static ICollectorDecorator Chain(ICollector collector)
        {
            ArgumentNullException.ThrowIfNull(collector, nameof(collector));

            return FromStrategy(async (next, context, token) =>
            {
                await collector.OnCollectAsync(context, token);
                await next(context, token);
            });
        }

        public static ICollector Empty()
        {
            return empty;
        }

        private record Decorator(OnCollectDecorator OnCollect) : ICollectorDecorator
        {
            private OnCollect onCollect;

            void IDecorator<ICollector>.Decorate(ICollector value)
            {
                onCollect = value.OnCollectAsync;
            }

            ValueTask ICollector.OnCollectAsync(IContext context, CancellationToken token)
            {
                return OnCollect != null ? OnCollect(onCollect, context, token) : onCollect(context, token);
            }
        }

        private record Instance(OnCollect OnCollect) : ICollector
        {
            ValueTask ICollector.OnCollectAsync(IContext context, CancellationToken token)
            {
                return OnCollect != null ? OnCollect(context, token) : default;
            }
        }
    }
}
