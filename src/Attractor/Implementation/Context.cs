using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Context
    {
        private const int DefaultCapacity = 16;

        public static IContext Empty { get; } = new EmptyContext();

        public static IContext Value<T>(T value) where T : class
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));

            return new ValueContext<T>(value);
        }

        public static IContext From(Action<IContextBuilder> configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

            var result = new DictionaryContext();

            configuration(result);

            return result;
        }

        public static IContext Override(IContext over, IContext with)
        {
            ArgumentNullException.ThrowIfNull(over, nameof(over));
            ArgumentNullException.ThrowIfNull(with, nameof(with));

            return new MergedContextDecorator(with, over);
        }

        public static IContext With<T>(this IContext context, T value) where T : class
        {
            return Override(context, Value(value));
        }

        public static IContext With(this IContext context, IContext other)
        {
            return Override(context, other);
        }

        public static IContext With(this IContext context, Action<IContextBuilder> configuration)
        {
            return Override(context, From(configuration));
        }

        public static IContext Registration(IAddressPolicy policy, Action<IActorBuilder> configuration = null)
        {
            ArgumentNullException.ThrowIfNull(policy, nameof(policy));

            var builder = new ActorBuilder();

            configuration?.Invoke(builder);

            return With(new RegisterMessage(policy, builder));
        }

        public static IContext Start(IAddress address)
        {
            ArgumentNullException.ThrowIfNull(address, nameof(address));

            return With(new StartMessage()).With(address);
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

        private class DefaultContext() : Dictionary<object, object>(DefaultCapacity), IContext
        {
            void IContext.ForEach(Action<KeyValuePair<object, object>> action)
            {
                foreach (var item in this)
                {
                    action(item);
                }
            }
        }

        private class SystemContext : DefaultContext;

        private class EmptyContext : IContext
        {
            T IContext.Get<T>() => null;
        }

        private class ValueContext<TValue>(TValue value) : IContext where TValue : class
        {
            T IContext.Get<T>() => typeof(T) == typeof(TValue) ? value as T : null;
        }

        private class DictionaryContext : Dictionary<Type, object>, IContextBuilder, IContext
        {
            void IContextBuilder.Set<T>(T value)
            {
                if (value == null)
                {
                    Remove(typeof(T));
                }
                else
                {
                    this[typeof(T)] = value;
                }
            }

            T IContext.Get<T>() => TryGetValue(typeof(T), out var value) ? (T)value : null;
        }

        private class MergedContextDecorator(IContext first, IContext second) : IContext
        {
            T IContext.Get<T>() => first.Get<T>() ?? second.Get<T>();
        }
    }
}