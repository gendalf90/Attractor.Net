namespace Attractor;

public interface IStage
{
    IProxy Play(IAddress address);
}
