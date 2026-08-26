namespace Attractor;

public interface IHandler : IAsyncDisposable
{
    Task OnReceive(IContext context, CancellationToken token = default);
}
