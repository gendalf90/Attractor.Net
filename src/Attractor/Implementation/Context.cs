using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Context
    {
        private const int DefaultCapacity = 16;
        
        public static IList Default()
        {
            return new DefaultContext();
        }

        public static IList System()
        {
            return new SystemContext();
        }

        public static IMessageFilter FromStrategy(OnMatch strategy)
        {
            ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

            return new StrategyMessageFilter(strategy);
        }

        public static bool IsSystem(IList context)
        {
            return context is SystemContext;
        }

        private record StrategyMessageFilter(OnMatch Strategy) : IMessageFilter
        {
            ValueTask<bool> IMessageFilter.IsMatchAsync(IList context, CancellationToken token)
            {
                return Strategy(context, token);
            }
        }

        private class DefaultContext() : List<object>(DefaultCapacity);

        private class SystemContext() : List<object>(DefaultCapacity);
    }
}