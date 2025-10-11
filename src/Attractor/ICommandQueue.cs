namespace Attractor;

internal interface ICommandQueue
{
    void Schedule(ICommand command);
}
