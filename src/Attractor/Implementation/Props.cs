using System;

namespace Attractor.Implementation;

public static class Props
{
    public static IProps From(Configure configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        return new StrategyInstance(configuration);
    }

    private class StrategyInstance(Configure configuration) : IProps
    {
        void IProps.Configure(IActorBuilder builder)
        {
            configuration(builder);
        }
    }
}