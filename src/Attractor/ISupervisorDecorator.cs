namespace Attractor
{
    public interface ISupervisorDecorator : ISupervisor, IDecorator<ISupervisor>
    {
    }
}