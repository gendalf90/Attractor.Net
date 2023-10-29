namespace Attractor.Tests.Unit
{
    public class ActorTests
    {
        [Fact]
        public async Task ActorSystem_SendMessage_MessageIsReceived()
        {
            // Arrange
            var competion = new TaskCompletionSource();
            var message = "message";
            var received = false;
            var system = ActorSystem.Create();
            var context = Context.Default();
            var address = Address.FromString("test");

            system.Register(Address.FromExact(address), builder =>
            {
                builder.Register(() => Actor.FromPayload<string>((str, _) => 
                {
                    received = str == message;

                    competion.SetResult();
                    
                    return default;
                }));
            });

            context.Set(Payload.From(message));
        
            // Act
            var actorRef = system.Refer(address);

            await actorRef.SendAsync(context);

            await competion.Task;
            
            // Assert
            Assert.True(received);
        }
    }
}