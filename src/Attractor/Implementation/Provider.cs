namespace Attractor.Implementation;

public static class Provider
{
    private static readonly AsyncLocal<IServiceProvider> CurrentProvider = new();

    public static IServiceProvider Current => CurrentProvider.Value;

    internal static IDisposable UseProvider(IServiceProvider provider)
    {
        var current = CurrentProvider.Value;

        CurrentProvider.Value = provider;

        return Disposable.Create(() => { CurrentProvider.Value = current; });
    }
}
