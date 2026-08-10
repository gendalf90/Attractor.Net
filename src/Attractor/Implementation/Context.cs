using System;
using System.Collections.Generic;

namespace Attractor.Implementation;

public static class Context
{    
    public static IContext Empty { get; } = new EmptyInstance();

    public static IContext Value<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        return new ValueInstance<T>(value);
    }

    public static IContext From(Action<IContextBuilder> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        var result = new DictionaryInstance();

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

    public static bool Exist<T>(this IContext context) where T : class
    {
        return context.Get<T>() is not null;
    }

    private class EmptyInstance : IContext
    {
        T IContext.Get<T>() => null;
    }

    private class ValueInstance<TValue>(object value) : IContext where TValue : class
    {
        T IContext.Get<T>() => typeof(T) == typeof(TValue) ? value as T : null;
    }

    private class DictionaryInstance : Dictionary<Type, object>, IContextBuilder, IContext
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

        T IContext.Get<T>() => TryGetValue(typeof(T), out var value) ? value as T : null;
    }

    private class MergedContextDecorator(IContext first, IContext second) : IContext
    {
        T IContext.Get<T>() => first.Get<T>() ?? second.Get<T>();
    }
}
