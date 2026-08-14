using Attractor.Benchmark.Utils;
using Attractor.Implementation;
using BenchmarkDotNet.Attributes;

namespace Attractor.Benchmark;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class ActorBenchmark
{
    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task SendMessagesToActor(int count)
    {
        var completed = false;
        var message = Message.Value(new LimitMessage(count));
        
        var actor = Actor.Run(Props.From(builder =>
        {
            builder.OnReceive<LimitMessage>(message =>
            {
                completed = !message.TryIncrease();
            });
        }));

        while (!completed)
        {
            await actor.Send(message);
        }
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task FireMessagesToActor(int count)
    {
        var completion = new TaskCompletionSource();
        
        var actor = Actor.Run(Props.From(builder =>
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

        actor.Fire(new PingMessage(actor, count));

        await completion.Task;
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task PingPongBetweenActors(int count)
    {
        var completion = new TaskCompletionSource();
        
        var pingActor = Actor.Run(Props.From(builder =>
        {
            builder.OnReceive<PingPongMessage>(message =>
            {
                if (message.TryIncrease())
                {
                    message.Pong.Fire(message);
                }
                else
                {
                    completion.SetResult();
                }
            });
        }));

        var pongActor = Actor.Run(Props.From(builder =>
        {
            builder.OnReceive<PingPongMessage>(message =>
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

        pingActor.Fire(new PingPongMessage(pingActor, pongActor, count));

        await completion.Task;
    }
}
