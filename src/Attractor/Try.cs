namespace Attractor;

public static class Try
{
    public static Try<T> True<T>(T result) => new() { Success = true, Result = result };

    public static Try<T> False<T>() => new();
}

public readonly struct Try<T>
{
    public bool Success { get; init; }

    public T Result { get; init; }
}