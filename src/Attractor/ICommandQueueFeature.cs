namespace Attractor;

public interface ICommandQueueFeature
{
    void Schedule(ICommand command);
}