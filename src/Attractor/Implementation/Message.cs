using System;

namespace Attractor.Implementation;

public static class Message
{
    public static IMessage From(Action<IContextBuilder> strategy)
    {
        return new Instance(strategy);
    }

    public static IMessage Merge(IMessage first, IMessage second)
    {
        return new MergedMessageDecorator(first, second);
    }

    public static IMessage With(this IMessage context, IMessage other)
    {
        return Merge(context, other);
    }

    public static IMessage With(this IMessage context, Action<IContextBuilder> configuration)
    {
        return Merge(context, From(configuration));
    }

    private class Instance(Action<IContextBuilder> strategy) : IMessage
    {
        void IMessage.Configure(IContextBuilder builder)
        {
            strategy(builder);
        }
    }

    private class MergedMessageDecorator(IMessage first, IMessage second) : IMessage
    {
        void IMessage.Configure(IContextBuilder builder)
        {
            first.Configure(builder);
            second.Configure(builder);
        }
    }
}