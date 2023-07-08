using System.Threading.Tasks;
using Attractor.Implementation;
using BenchmarkDotNet.Attributes;
using Proto;

namespace Attractor.Benchmark
{
    [SimpleJob(launchCount: 3, warmupCount: 3, iterationCount: 3, invocationCount: 3)]
    [MemoryDiagnoser]
    public class Messaging
    {
        [Params(100000, 1000000, 10000000)]
        public int Count { get; set; }

        [Benchmark]
        public async Task RunAttractor()
        {
            var completion = new TaskCompletionSource();
            var system = Implementation.ActorSystem.Create();
            var pingContext = Context.Default();
            var pongContext = Context.Default();
            var payload = Payload.FromString("test");

            system.Register(Address.FromString(addr => addr == "pong"), builder =>
            {
                var counter = 0;
                
                builder.RegisterActor(() => Actor.FromString((_, _) => 
                {
                    if (++counter == Count)
                    {
                        completion.SetResult();
                    }

                    return default;
                }));
            });

            var pong = system.Refer(Address.FromString("pong"));

            pongContext.Set(payload);

            system.Register(Address.FromString(addr => addr == "ping"), builder =>
            {
                builder.RegisterActor(() => Actor.FromStrategy((context, token) => 
                {
                    return pong.PostAsync(pongContext, token);
                }));
            });

            var ping = system.Refer(Address.FromString("ping"));

            pingContext.Set(payload);
            
            for (int i = 0; i < Count; i++)
            {
                await ping.PostAsync(pingContext);
            }

            await completion.Task;
        }
        
        [Benchmark]
        public async Task RunAkka()
        {
            var completion = new TaskCompletionSource();
            var system = Akka.Actor.ActorSystem.Create("PingPong");
            var pong = system.ActorOf(Akka.Actor.Props.Create(() => new PongAkkaActor(completion, Count)));
            var ping = system.ActorOf(Akka.Actor.Props.Create(() => new PingAkkaActor(pong)));
            
            for (int i = 0; i < Count; i++)
            {
                Akka.Actor.ActorRefImplicitSenderExtensions.Tell(ping, "test");
            }

            await completion.Task;
        }

        [Benchmark]
        public async Task RunProtoActor()
        {
            var completion = new TaskCompletionSource();
            var system = new Proto.ActorSystem();
            var pongProps = Props.FromProducer(() => new PongProtoActor(completion, Count));
            var pingProps = Props.FromProducer(() => new PingProtoActor(system.Root.Spawn(pongProps)));
            var ping = system.Root.Spawn(pingProps);

            for (int i = 0; i < Count; i++)
            {
                system.Root.Send(ping, "test");
            }

            await completion.Task;
        }

        private class PingAkkaActor : Akka.Actor.ReceiveActor
        {
            public PingAkkaActor(Akka.Actor.IActorRef pong)
            {
                Receive<string>(message =>
                {
                    Akka.Actor.ActorRefImplicitSenderExtensions.Tell(pong, message);
                });
            }
        }

        private class PongAkkaActor : Akka.Actor.ReceiveActor
        {
            private int counter;
            
            public PongAkkaActor(TaskCompletionSource completion, int count)
            {
                Receive<string>(message =>
                {
                    if (++counter == count)
                    {
                        completion.SetResult();
                    }
                });
            }
        }

        private class PingProtoActor : Proto.IActor
        {
            private readonly Proto.PID pong;

            public PingProtoActor(Proto.PID pong)
            {
                this.pong = pong;
            }

            public Task ReceiveAsync(Proto.IContext context)
            {
                switch (context.Message)
                {
                    case string msg:
                        context.Send(pong, msg);
                        break;
                }

                return Task.CompletedTask;
            }
        }

        private class PongProtoActor : Proto.IActor
        {
            private readonly TaskCompletionSource completion;
            private readonly int count;

            public PongProtoActor(TaskCompletionSource completion, int count)
            {
                this.completion = completion;
                this.count = count;
            }

            private int counter;
            
            public Task ReceiveAsync(Proto.IContext context)
            {
                switch (context.Message)
                {
                    case string:
                        counter++;
                        break;
                }

                if (counter == count)
                {
                    completion.SetResult();
                }

                return Task.CompletedTask;
            }
        }
    }
}