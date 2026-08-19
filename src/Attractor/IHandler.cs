namespace Attractor;

public interface IHandler
{
    Task OnReceive(IContext context, CancellationToken token = default);
}
