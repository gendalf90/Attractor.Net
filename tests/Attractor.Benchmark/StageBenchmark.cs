using Attractor.Benchmark.Utils;
using Attractor.Implementation;
using BenchmarkDotNet.Attributes;

namespace Attractor.Benchmark;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class StageBenchmark
{
    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task SendMessagesToStage(int count)
    {
        var completed = false;
        var address = Address.FromString("test");
        var message = Message.Value(new LimitMessage(count));

        await using var stage = Stage.Run(registry =>
        {
            registry.Register(Address.FromExact(address), Props.From(builder =>
            {
                builder.OnReceive<LimitMessage>(message =>
                {
                    completed = !message.TryIncrease();
                });
            }));
        });

        await using var proxy = stage.Play(address);

        while (!completed)
        {
            await proxy.Send(message);
        }
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task ShootMessagesToStage(int count)
    {
        var completed = false;
        var address = Address.FromString("test");
        var message = Message.Value(new LimitMessage(count));

        await using var stage = Stage.Run(registry =>
        {
            registry.Register(Address.FromExact(address), Props.From(builder =>
            {
                builder.OnReceive<LimitMessage>(message =>
                {
                    completed = !message.TryIncrease();
                });
            }));
        });

        while (!completed)
        {
            await stage.Shoot(address, message);
        }
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task FireMessagesToStage(int count)
    {
        var completion = new TaskCompletionSource();
        var address = Address.FromString("test");

        await using var stage = Stage.Run(registry =>
        {
            registry.Register(Address.FromExact(address), Props.From(builder =>
            {
                builder.OnReceive<PingMessage>(message =>
                {
                    if (message.TryIncrease())
                    {
                        message.Ping.Fire(message);
                    }
                    else
                    {
                        completion.SetResult();
                    }
                });
            }));
        });

        await using var proxy = stage.Play(address);

        proxy.Fire(new PingMessage(proxy, count));

        await completion.Task;
    }
}
