using System;

namespace Attractor.Implementation;

public static class Message
{
    public static IMessage Empty { get; } = new EmptyInstance();
    
    public static IMessage Value<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        return new ValueInstance<T>(value);
    }

    public static IMessage From(Action<IContextBuilder> strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        return new StrategyInstance(strategy);
    }

    private class EmptyInstance : IMessage
    {
        void IMessage.Configure(IContextBuilder builder) { }
    }

    private class ValueInstance<T>(T value) : IMessage where T : class
    {
        void IMessage.Configure(IContextBuilder builder)
        {
            builder.Set(value);
        }
    }

    private class StrategyInstance(Action<IContextBuilder> strategy) : IMessage
    {
        void IMessage.Configure(IContextBuilder builder)
        {
            strategy(builder);
        }
    }
}
