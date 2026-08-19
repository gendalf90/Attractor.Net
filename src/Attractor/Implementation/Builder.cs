namespace Attractor.Implementation;

internal class Builder<TResult>(TResult value) : IBuilder<TResult>
{
    private Func<TResult, TResult> decoratorFactory = _ => _;

    void IBuilder<TResult>.Decorate<T>(Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));

        decoratorFactory = Decorate(decoratorFactory, factory);
    }

    public TResult Build()
    {
        return decoratorFactory(value);
    }

    private static Func<TResult, TResult> Decorate<TDecorator>(Func<TResult, TResult> resultFactory, Func<TDecorator> decoratorFactory)
        where TDecorator : class, TResult, IDecorator<TResult>
    {
        return decoratee =>
        {
            var result = resultFactory(decoratee);
            var decorator = decoratorFactory();

            decorator.Decorate(result);

            return decorator;
        };
    }
}
