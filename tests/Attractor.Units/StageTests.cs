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
        await stage.Shoot(address, message);
        
        // Assert
        Assert.True(received);
    }

    [Fact]
    public async Task Stage_SendMessage_MessageIsReceivedInCommonHandler()
    {
        // Arrange
        var message = "test";
        var received = false;
        var address = Address.FromString("test");
        var stage = Stage.Run(registry =>
        {
            registry.OnReceive<string>(value => received = value == message);
            registry.Register(Address.FromExact(address), Props.Empty);
        });
    
        // Act
        await stage.Shoot(address, message);
        
        // Assert
        Assert.True(received);
    }

    [Fact]
    public async Task Stage_SendMessage_ErrorIfActorIsNotRegistered()
    {
        // Arrange
        var stage = Stage.Run(registry => { });
    
        // Act
        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => stage.Shoot("test", "test"));
    }

    [Fact]
    public async Task Stage_Cancel_StageIsCancelled()
    {
        // Arrange
        var stageSource = new CancellationTokenSource();
        var messageSource = new CancellationTokenSource();
        var address = Address.FromString("test");
        var stage = Stage.Run(registry =>
        {
            registry.Register(Address.FromExact(address), Props.Empty);
        }, stageSource.Token);
    
        // Act
        // Assert
        messageSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stage.Shoot(address, "test", messageSource.Token));

        stageSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stage.Shoot(address, "test"));
    }

    [Fact]
    public async Task Stage_SendMessage_MessagesAreReceivedInSendingOrder()
    {
        // Arrange
        var received = new List<string>();
        var address = Address.FromString("test");
        var stage = Stage.Run(registry =>
        {
            registry.Register(Address.FromExact(address), Props.From(builder =>
            {
                builder.OnReceive<string>(received.Add);
            }));
        });
    
        // Act
        var proxy = stage.Play(address);
        
        await using (proxy)
        {
            proxy.Fire("1");
            proxy.Fire("2");
            proxy.Fire("3");
            proxy.Fire("4");
            proxy.Fire("5");
        }

        // Assert
        Assert.Equal(received, ["1", "2", "3", "4", "5"]);
    }

    [Fact]
    public async Task Stage_SendMessage_ContextHasAddressAndStageReferences()
    {
        // Arrange
        var nullBeforeSend = false;
        var nullAfterSend = false;
        var hasInStatic = false;
        var address = Address.FromString("test");
        var stage = Stage.Run(registry =>
        {
            registry.Register(Address.FromExact(address), Props.From(builder =>
            {
                builder.OnReceive(context =>
                {
                    hasInStatic = Stage.Current != null && Address.Current != null;
                });
            }));
        });
    
        // Act
        nullBeforeSend = Stage.Current == null && Address.Current == null;
        
        await stage.Shoot(address, "test");

        nullAfterSend = Stage.Current == null && Address.Current == null;
        
        // Assert
        Assert.True(nullBeforeSend);
        Assert.True(hasInStatic);
        Assert.True(nullAfterSend);
    }

    [Fact]
    public async Task Stage_SendMessage_ThrowProcessingException()
    {
        // Arrange
        var address = Address.FromString("test");
        var stage = Stage.Run(registry =>
        {
            registry.Register(Address.FromExact(address), Props.From(builder =>
            {
                builder.OnReceive(_ => throw null);
            }));
        });
    
        // Act
        // Assert
        await Assert.ThrowsAsync<NullReferenceException>(() => stage.Shoot(address, "test"));
    }

    [Fact]
    public async Task Stage_SendMessage_ActorIsCancelledAfterProxyIsDisposed()
    {
        // Arrange
        var address = Address.FromString("test");
        var counter = 0;
        var stage = Stage.Run(registry =>
        {
            registry.Register(Address.FromExact(address), Props.From(builder =>
            {
                builder.OnReceive(_ => 
                {
                    Actor.Cancellation.Token.Register(() =>
                    {
                        counter++;
                    });
                });
            }));
        });
    
        // Act
        await stage.Shoot(address, "test");
        await stage.Shoot(address, "test");
        
        // Assert
        Assert.Equal(2, counter);
    }
}
