namespace Attractor.Units;

public class SystemTests
{
    [Fact]
    public async Task Stage_SendMessage_MessageIsReceived()
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
    
        // Act
        using var proxy = stage.Play(address);

        await proxy.Send("test");
        
        // Assert
        Assert.True(received);
    }
}