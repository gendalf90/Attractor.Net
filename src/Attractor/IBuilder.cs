namespace Attractor;

public interface IBuilder<TResult>
{
    void Decorate<T>(Func<T> factory) where T : class, TResult, IDecorator<TResult>;
}