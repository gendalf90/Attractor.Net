namespace Attractor
{
    internal interface IActorFactory
    {
        IActorCreator UseCreator();

        IActorPool CreatePool();

        IAddressPolicy CreateAddressPolicy();

        IActorExecutor CreateExecutor();
    }
}
