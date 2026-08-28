namespace Attractor;

public interface IReceiver<T> where T : class
{
    Task OnReceive(T value, CancellationToken token = default);
}
