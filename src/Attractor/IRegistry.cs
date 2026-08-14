namespace Attractor;

public interface IRegistry : IBuilder<IHandler>
{
    void Register(IRouter router, IProps props);
}
