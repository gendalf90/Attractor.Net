namespace Attractor
{
    public interface IRequest : IPayload, ICancellation, ICompletion
    {
    }
}
