namespace Attractor;

public interface ICommandQueue
{
    void Schedule(ICommand command);
}