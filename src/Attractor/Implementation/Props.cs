using System;

namespace Attractor.Implementation;

public static class Props
{
    public static IProps From(Action<IActorBuilder> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        return new StrategyInstance(configuration);
    }

    private class StrategyInstance(Action<IActorBuilder> configuration) : IProps
    {
        void IProps.Configure(IActorBuilder builder)
        {
            configuration(builder);
        }
    }
}