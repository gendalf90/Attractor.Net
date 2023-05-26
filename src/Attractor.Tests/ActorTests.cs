namespace Attractor.Tests
{
    public class ActorTests
    {
        [Fact]
        public async ValueTask ActorSystem_SendMessage_MessageIsReceived()
        {
            // Arrange
            var message = "message";
            var received = false;
            var system = ActorSystem.Create();

            system.Register(Address.FromString(addr => addr == "test"), builder =>
            {
                builder.RegisterActor(() => Actor.FromString((str, _) => 
                {
                    received = str == message;
                    
                    return default;
                }));
            });
        
            // Act
            var actorRef = await system.GetRefAsync(Address.FromString("test"));

            await actorRef.SendAsync(Payload.FromString(message));
        
            // Assert
            Assert.True(received);
        }
    }
}