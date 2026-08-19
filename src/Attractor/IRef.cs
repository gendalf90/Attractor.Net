namespace Attractor;

public interface IRef
{
    Task Send(IMessage message, CancellationToken token = default);
}
