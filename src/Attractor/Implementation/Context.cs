using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Context
    {
        private const int DefaultCapacity = 16;
        
        public static IContext Default()
        {
            return new DefaultContext();
        }

        public static IContext System()
        {
            return new SystemContext();
        }

        public static IMessageFilter FromStrategy(OnMatch strategy)
        {
            ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

            return new StrategyMessageFilter(strategy);
        }

        public static IMessageFilter FromStrategy(Predicate<IContext> strategy)
        {
            ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

            return FromStrategy((context, _) => ValueTask.FromResult(strategy(context)));
        }

        public static IMessageFilter IsSystem()
        {
            return FromStrategy(context => context is SystemContext);
        }

        private record StrategyMessageFilter(OnMatch Strategy) : IMessageFilter
        {
            ValueTask<bool> IMessageFilter.IsMatchAsync(IContext context, CancellationToken token)
            {
                return Strategy(context, token);
            }
        }

        private class DefaultContext() : Dictionary<object, object>(DefaultCapacity), IContext;

        private class SystemContext() : Dictionary<object, object>(DefaultCapacity), IContext;
    }
}