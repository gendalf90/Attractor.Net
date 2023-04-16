namespace Attractor
{
    internal readonly struct TryResult<T>
    {
        public bool Success { init; get; }

        public T Value { init; get; }

        public static TryResult<T> True(T value) => new()
        {
            Value = value,
            Success = true
        };

        public static TryResult<T> False(T value = default) => new()
        {
            Value = value,
            Success = false
        };
    }
}
