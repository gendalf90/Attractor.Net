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

            system.Register(Address.FromExact(address), Actor.FromPayload<string>(str => 
            {
                received = str == message;

                competion.SetResult();
            }).Register());

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