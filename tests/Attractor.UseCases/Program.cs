using System.Reflection;
using Attractor;
using Attractor.Implementation;
using Microsoft.Extensions.DependencyInjection;

// begin-snippet: SimpleActorUsage
var actor1 = Actor.Run(Props.From(builder =>
{
    builder.OnReceive<string>(value => Console.WriteLine(value));
    builder.OnDispose(() => Console.WriteLine("dispose"));
}));

await using (actor1)
{
    await actor1.Send("value");
}
// end-snippet

// begin-snippet: SimpleStageUsage
var address1 = Address.FromString("address");
var system1 = Stage.Run(registry =>
{
    registry.Register(Address.FromExact(address1), Props.From(builder =>
    {
        builder.OnReceive<string>(value => Console.WriteLine(value));
        builder.OnDispose(() => Console.WriteLine("dispose"));
    }));
});

await using (system1)
{
    await using var proxy = system1.Play(address1);

    await proxy.Send("value");
}
// end-snippet

// begin-snippet: ExtendedStageUsage
var services1 = new ServiceCollection();

services1.AddStage(registry =>
{
    registry.OnReceive<string>(value => Console.WriteLine(value));
    registry.OnDispose(() => Console.WriteLine("dispose"));
    registry.Register(Address.FromStrategy(value => value.StartsWith("actor/")), Props.Empty);
});

var provider1 = services1.BuildServiceProvider();

await using (provider1)
{
    var stage = provider1.GetRequiredService<IStage>();

    await using var proxy1 = stage.Play(Address.FromString("actor/1"));
    await using var proxy2 = stage.Play(Address.FromString("actor/1"));
    await using var proxy3 = stage.Play(Address.FromString("actor/2"));

    await proxy1.Send("value1");
    await proxy2.Send("value2");
    await proxy3.Send("value3");
}
// end-snippet

// begin-snippet: SimpleCacheUsage
var system2 = Stage.Run(registry =>
{
    registry.Register(Address.FromStrategy(value => value.StartsWith("actor/")), Props.From(builder =>
    {
        builder.OnReceive<string>(value => Console.WriteLine(value));
        builder.OnDispose(() => Console.WriteLine("dispose"));
    }));
});

await using (system2)
{
    var cache = Cache.Round(system2, 1);

    var ref1 = cache.Get(Address.FromString("actor/1"));

    await ref1.Send("value1");

    var ref2 = cache.Get(Address.FromString("actor/2"));

    await ref2.Send("value2");
}
// end-snippet

// begin-snippet: ConcurrencyUsage
var actor2 = Actor.Run(Props.From(builder =>
{
    builder.OnReceive(async (_, token) =>
    {
        Console.WriteLine(SynchronizationContext.Current?.GetType().Name);
        
        var task1 = Task.Factory.StartNew(() =>
        {
            Console.WriteLine(SynchronizationContext.Current?.GetType().Name);
        }, token, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

        var task2 = Task.Factory.StartNew(() =>
        {
            Console.WriteLine(SynchronizationContext.Current?.GetType().Name);
        }, token, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

        await Task.WhenAll(task1, task2);
    });
}));

await using (actor2)
{
    await actor2.Send(Message.Empty);
}
// end-snippet

// begin-snippet: AssemblyStageUsage
var services2 = new ServiceCollection();

services2.AddActors(Assembly.GetExecutingAssembly());
services2.AddStage(registry => registry.UseActors());

var provider2 = services2.BuildServiceProvider();

await using (provider2)
{
    var stage = provider2.GetRequiredService<IStage>();

    await using var proxy = stage.Play(Address.FromString(nameof(TestHandler)));

    await proxy.Send("value");
}

public class TestHandler : IReceiver<string>, IDisposable
{
    public Task OnReceive(string value, CancellationToken token = default)
    {
        Console.WriteLine(value);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Console.WriteLine("dispose");
    }
}
// end-snippet
