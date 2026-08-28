namespace Attractor;

public interface IRouter
{
    bool IsMatch(IAddress address);
}
