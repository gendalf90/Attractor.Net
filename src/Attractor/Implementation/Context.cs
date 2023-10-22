using System;
using System.Collections;
using System.Collections.Generic;

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

        public static IMessageFilter FromStrategy(Predicate<IList> strategy)
        {
            ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

            return new StrategyMessageFilter(strategy);
        }

        public static IMessageFilter OnlySystem()
        {
            return FromStrategy(context =>
            {
                return context is SystemContext;
            });
        }

        private record StrategyMessageFilter(Predicate<IList> Strategy) : IMessageFilter
        {
            bool IMessageFilter.IsMatch(IList context)
            {
                return Strategy(context);
            }
        }

        private class DefaultContext() : List<object>(DefaultCapacity);

        private class SystemContext() : List<object>(DefaultCapacity);
    }
}