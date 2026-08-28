namespace Attractor;

public interface ICache
{
    IRef Get(IAddress address);
}
