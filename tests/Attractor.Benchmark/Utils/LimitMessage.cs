namespace Attractor.Benchmark.Utils;

public record LimitMessage(int Limit)
{
    private int counter = 0;

    public bool TryIncrease() => ++counter < Limit;
}
