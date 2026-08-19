using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Attractor.Units;

public class RegistrationTests
{
    [Fact]
    public async Task Stage_RegisterInServices_ActorIsRegistered()
    {
        // Arrange
        var counter = 0;
        var address = Address.FromString("test");
        var services = new ServiceCollection();

        services.AddTransient(_ => new TestHandler(_ => counter++));
        services.AddStage(registry =>
        {
            registry.Handle<TestHandler>();
            registry.Register(Address.FromExact(address), Props.From(builder =>
            {
                builder.Handle<TestHandler>();
            }));
        });

        using var provider = services.BuildServiceProvider();

        var stage = provider.GetRequiredService<IStage>();
    
        // Act
        await stage.Shoot(address, "test");
        
        // Assert
        Assert.Equal(2, counter);
    }

    private class TestHandler(Receive strategy) : IHandler
    {
        public Task OnReceive(IContext context, CancellationToken token)
        {
            strategy(context);

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Stage_RegisterFromAssembly_ActorIsRegistered()
    {
        // Arrange
        var results = new List<string>();
        var address = Address.FromString(nameof(AssemblyTestHandler));
        var services = new ServiceCollection();

        services.AddSingleton(results);
        services.AddActors(Assembly.GetExecutingAssembly());
        services.AddTransient(_ => new TestHandler(_ => results.Add("test")));
        services.AddStage(registry => registry.Handle<TestHandler>());

        using var provider = services.BuildServiceProvider();

        var stage = provider.GetRequiredService<IStage>();
    
        // Act
        await stage.Shoot(address, Tuple.Create(1));
        await stage.Shoot(address, Tuple.Create(0.5));
        
        // Assert
        Assert.Equal(4, results.Count(value => value == "test"));
        Assert.Equal(2, results.Count(value => value == "context"));
        Assert.Equal(1, results.Count(value => value == "int"));
        Assert.Equal(1, results.Count(value => value == "double"));
    }

    public class AssemblyTestHandler(List<string> results) : IHandler, IReceiver<Tuple<int>>, IReceiver<Tuple<double>>, IProps
    {
        public void Configure(IBuilder<IHandler> builder)
        {
            builder.Handle<TestHandler>();
        }

        public Task OnReceive(IContext context, CancellationToken token)
        {
            results.Add("context");

            return Task.CompletedTask;
        }

        public Task OnReceive(Tuple<int> value, CancellationToken token = default)
        {
            results.Add("int");

            return Task.CompletedTask;
        }

        public Task OnReceive(Tuple<double> value, CancellationToken token = default)
        {
            results.Add("double");

            return Task.CompletedTask;
        }
    }
}
