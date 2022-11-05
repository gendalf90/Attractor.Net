namespace Attractor
{
    public interface IAddressPolicy
    {
        bool IsMatch(IAddress address);
    }
}
