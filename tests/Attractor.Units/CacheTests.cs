namespace Attractor.Units;

public class CacheTests
{
    [Fact]
    public async Task RoundCache_SendMessage_MessageIsReceived()
    {
        // Arrange
        var message = "test";
        var received = false;
        var address = Address.FromString("test");
        var stage = Stage.Run(registry =>
        {
            registry.Register(Address.FromExact(address), Props.From(builder =>
            {
                builder.OnReceive<string>(value => received = value == message);
            }));
        });
        var cache = Cache.Round(stage, 1);
    
        // Act
        await cache.Get(address).Send("test");
        
        // Assert
        Assert.True(received);
    }

    [Fact]
    public async Task RoundCache_SendMessage_ActorIsCancelledIfDisplaced()
    {
        // Arrange
        var addressOne = Address.FromString("test1");
        var addressTwo = Address.FromString("test2");
        var canceled = false;
        var stage = Stage.Run(registry =>
        {
            registry.Register(Address.FromExact(addressOne), Props.From(builder =>
            {
                builder.OnReceive(_ => 
                {
                    Actor.Cancellation.Token.Register(() =>
                    {
                        canceled = true;
                    });
                });
            }));
            registry.Register(Address.FromExact(addressTwo), Props.Empty);
        });
        var cache = Cache.Round(stage, 1);
    
        // Act
        await cache.Get(addressOne).Send("test");
        await cache.Get(addressTwo).Send("test");
        
        // Assert
        Assert.True(canceled);
    }
}
