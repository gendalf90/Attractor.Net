namespace Attractor.Units;

public class ActorTests
{
    [Fact]
    public async Task Actor_SendMessage_MessageIsReceived()
    {
        // Arrange
        var message = "test";
        var received = false;
        var actor = Actor.Run(Props.From(builder =>
        {
            builder.OnReceive<string>(value => received = value == message);
        }));
    
        // Act
        await actor.Send(message);
        
        // Assert
        Assert.True(received);
    }

    [Fact]
    public async Task Actor_Cancel_ActorIsCancelled()
    {
        // Arrange
        var actorSource = new CancellationTokenSource();
        var messageSource = new CancellationTokenSource();
        var actor = Actor.Run(Props.Empty, actorSource.Token);
    
        // Act
        // Assert
        messageSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => actor.Send("test", messageSource.Token));

        actorSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => actor.Send("test"));

        Assert.True(actor.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Actor_DecorateReceiving_TheOrderOfReceivingIsRight()
    {
        // Arrange
        var received = new List<int>();
        var actor = Actor.Run(Props.From(builder =>
        {
            builder.OnReceive(_ => received.Add(2));
            builder.OnReceive(async (next, context, token) =>
            {
                received.Add(1);
                await next(context, token);
                received.Add(3);
            });
            builder.OnReceive(_ => received.Add(4));
        }));
    
        // Act
        await actor.Send("test");
        
        // Assert
        Assert.Equal(received, [1, 2, 3, 4]);
    }

    [Fact]
    public async Task Actor_SendMessage_ContextHasCancellation()
    {
        // Arrange
        var nullBeforeSend = false;
        var nullAfterSend = false;
        var hasInStatic = false;
        var actor = Actor.Run(Props.From(builder =>
        {
            builder.OnReceive(context =>
            {
                hasInStatic = Actor.Cancellation != null;
            });
        }));
    
        // Act
        nullBeforeSend = Actor.Cancellation == null;

        await actor.Send("test");

        nullAfterSend = Actor.Cancellation == null;
        
        // Assert
        Assert.True(nullBeforeSend);
        Assert.True(hasInStatic);
        Assert.True(nullAfterSend);
    }

    [Fact]
    public async Task Actor_SendMessage_MessagesAreProcessedInOrder()
    {
        // Arrange
        var processing = 0;
        var actor = Actor.Run(Props.From(builder =>
        {
            builder.OnReceive(async (context, token) =>
            {
                if (Interlocked.Increment(ref processing) > 1)
                {
                    throw new Exception();
                }

                await Task.Delay(1, token);

                Interlocked.Decrement(ref processing);
            });
        }));

        Task Send()
        {
            return actor.Send("test");
        }

        // Act
        // Assert
        await Task.WhenAll([Send(), Send(), Send(), Send(), Send()]);
    }

    [Fact]
    public async Task Actor_SendMessage_ThrowProcessingException()
    {
        // Arrange
        var actor = Actor.Run(Props.From(builder =>
        {
            builder.OnReceive(_ => throw null);
        }));
    
        // Act
        // Assert
        await Assert.ThrowsAsync<NullReferenceException>(() => actor.Send("test"));
    }
}
