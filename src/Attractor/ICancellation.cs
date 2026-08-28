namespace Attractor;

public interface ICancellation
{
    CancellationToken Token { get; }
}
