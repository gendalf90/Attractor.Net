namespace Attractor
{
    public readonly struct Try<T>
    {
        public bool Success { init; get; }

        public T Value { init; get; }

        public static Try<T> True(T value) => new()
        {
            Value = value,
            Success = true
        };

        public static Try<T> False(T value = default) => new()
        {
            Value = value,
            Success = false
        };
    }
}
