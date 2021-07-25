namespace Attractor
{
    public abstract class TryResult<T>
    {
        public bool IsSuccess { protected set; get; }

        public T Value { protected set; get; }

        public static implicit operator bool(TryResult<T> result) => result.IsSuccess;
    }

    public sealed class TrueResult<T> : TryResult<T>
    {
        public TrueResult(T value)
        {
            IsSuccess = true;
            Value = value;
        }
    }

    public sealed class FalseResult<T> : TryResult<T>
    {
        public FalseResult()
        {
            IsSuccess = false;
            Value = default;
        }
    }
}
