# ![Logo](icon.png) Attractor

[![NuGet](https://img.shields.io/nuget/v/Attractor.svg)](https://www.nuget.org/packages/Attractor/)

Actor model system implementation for .NET

Do you remember WinForms? Each form has synchronization context and every action has to be performed within this context. You do not have to constantly think about state synchronization even when you use asynchronous methods. So there has arisen an idea - why would not make a system where every object has its own synchronization context and you can be confident about state safe while calling methods and even using task parallelization within it.

### What is it

This library is one more trying to implement actors in .Net. Each actor keeps its own state and processes messages sequentially. You can be sure to use actor reference from different threads safe. Also each actor has synchronization context and it is possible to run many tasks during message processing without additional state synchronization. You can think about it as another way to register and call named expandable stateful thread safe handlers in your code.

### How to start using

Install the package latest version from [NuGet](https://www.nuget.org/packages/Attractor/) and have fun!

### Show me the code

To create a simple actor and send message to it just write:
<!-- snippet: SimpleActorUsage -->
<a id='snippet-SimpleActorUsage'></a>
```cs
var actor1 = Actor.Run(Props.From(builder =>
{
    builder.OnReceive<string>(value => Console.WriteLine(value));
    builder.OnDispose(() => Console.WriteLine("dispose"));
}));

await using (actor1)
{
    await actor1.Send("value");
}
```
<sup><a href='/tests/Attractor.UseCases/Program.cs#L6-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-SimpleActorUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Also you can unite many actors in system and get references on them by address. 
<!-- snippet: SimpleStageUsage -->
<a id='snippet-SimpleStageUsage'></a>
```cs
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
```
<sup><a href='/tests/Attractor.UseCases/Program.cs#L19-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-SimpleStageUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

There are extensions for registration system in DI. As you can see there is using an address template in actor registration. This means that actors are created by one template that is fit for an address. And if you run the code you will see that *dispose* will be called only twice. This happens because the system uses reference counters for created proxy by the same address. An actor exists within the system while there are not disposed proxies for it. Also you can register common handlers for all actors within the system.
<!-- snippet: ExtendedStageUsage -->
<a id='snippet-ExtendedStageUsage'></a>
```cs
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
```
<sup><a href='/tests/Attractor.UseCases/Program.cs#L38-L62' title='Snippet source file'>snippet source</a> | <a href='#snippet-ExtendedStageUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

As you could see at last example the lifetime of system actor proxy depends on alive references. So there is round cache implementation which manages automatically proxy lifetime and keeps references active within set capacity (you can make you own strategy for it). The *ref1* disposes automatically when sending on *ref2* is calling.
<!-- snippet: SimpleCacheUsage -->
<a id='snippet-SimpleCacheUsage'></a>
```cs
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
```
<sup><a href='/tests/Attractor.UseCases/Program.cs#L64-L86' title='Snippet source file'>snippet source</a> | <a href='#snippet-SimpleCacheUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Tasks which were run within processing messages or any async methods are executing in `StrandingSynchronizationContext` that puts them in queue and processes sequentially without state parallel accessing. So you do not need any thread synchronization. Async methods that run in parallel are executing concurrently in parts separated by state machine.
<!-- snippet: ConcurrencyUsage -->
<a id='snippet-ConcurrencyUsage'></a>
```cs
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
```
<sup><a href='/tests/Attractor.UseCases/Program.cs#L88-L113' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConcurrencyUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

You do not have to use manual actor registration and it is possible to describe actor by using interface implementation. All actors will be registered by reflection from specific assembly. The class must implement either `IHandler` or some `IReceiver<T>` interfaces. Also interfaces `IAsyncDisposable`, `IDisposable`, `IProps` are available for extention.
<!-- snippet: AssemblyStageUsage -->
<a id='snippet-AssemblyStageUsage'></a>
```cs
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
```
<sup><a href='/tests/Attractor.UseCases/Program.cs#L115-L146' title='Snippet source file'>snippet source</a> | <a href='#snippet-AssemblyStageUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Boring part

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

 
