using System;

namespace Attractor.Implementation;

internal static class Command
{
    public static ICommand Create(Action strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        return new StrategyCommand(strategy);
    }

    private record StrategyCommand(Action Strategy) : ICommand
    {
        void ICommand.Execute()
        {
            Strategy();
        }
    }
}