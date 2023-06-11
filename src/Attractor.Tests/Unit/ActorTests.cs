namespace Attractor.Tests.Unit
{
    public class ActorTests
    {
        [Fact]
        public async Task ActorSystem_SendMessage_MessageIsReceived()
        {
            // Arrange
            var message = "message";
            var received = false;
            var system = ActorSystem.Create();
            var context = Context.Default();

            system.Register(Address.FromString(addr => addr == "test"), builder =>
            {
                builder.RegisterActor(() => Actor.FromString((str, _) => 
                {
                    received = str == message;
                    
                    return default;
                }));
            });

            context.Set(Payload.FromString(message));
        
            // Act
            var actorRef = system.Refer(Address.FromString("test"));

            await actorRef.SendAsync(context);
            
            // Assert
            Assert.True(received);
        }
    }
}