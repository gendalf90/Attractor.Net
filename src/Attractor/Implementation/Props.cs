using System;

namespace Attractor.Implementation;

public static class Props
{
    public static IProps Empty { get; } = new EmptyInstance();
    
    public static IProps From(Configure configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        return new StrategyInstance(configuration);
    }

    public static IProps Decorate(this IProps props, DecorateConfigure configuration)
    {
        ArgumentNullException.ThrowIfNull(props, nameof(props));
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        return new DecoratorInstance(props, configuration);
    }

    public static IProps With(this IProps first, IProps second)
    {
        ArgumentNullException.ThrowIfNull(first, nameof(first));
        ArgumentNullException.ThrowIfNull(second, nameof(second));

        return From(builder =>
        {
            first.Configure(builder);
            second.Configure(builder);
        });
    }

    private class EmptyInstance : IProps
    {
        public void Configure(IBuilder<IHandler> builder) {}
    }

    private class DecoratorInstance(IProps props, DecorateConfigure configure) : IProps
    {
        void IProps.Configure(IBuilder<IHandler> builder)
        {
            configure(props.Configure, builder);
        }
    }

    private class StrategyInstance(Configure configuration) : IProps
    {
        void IProps.Configure(IBuilder<IHandler> builder)
        {
            configuration(builder);
        }
    }
}
