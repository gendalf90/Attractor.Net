namespace Attractor;

public interface IRegistry
{
    void Register(IRouter router, IProps props);
}
