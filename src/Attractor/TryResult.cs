namespace Attractor
{
    public readonly struct TryResult<T>
    {
        public bool Success { init; get; }

        public T Value { init; get; }

        public static TryResult<T> True(T value) => new TryResult<T>
        {
            Value = value,
            Success = true
        };

        public static TryResult<T> False(T value = default) => new TryResult<T>
        {
            Value = value,
            Success = false
        };
    }
}
