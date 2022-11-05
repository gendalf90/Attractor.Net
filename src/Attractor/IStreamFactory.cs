namespace Attractor
{
    internal interface IStreamFactory
    {
        IScopedStreamHandler CreateStream();

        IAddressPolicy CreateAddressPolicy();
    }
}
