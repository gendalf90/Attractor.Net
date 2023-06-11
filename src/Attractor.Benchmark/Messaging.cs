using System.Threading.Tasks;
using Attractor.Implementation;
using BenchmarkDotNet.Attributes;

namespace Attractor.Benchmark
{
    [SimpleJob(launchCount: 3, warmupCount: 3, iterationCount: 3, invocationCount: 3)]
    [MemoryDiagnoser]
    public class Messaging
    {
        [Params(100000)]
        public int Count { get; set; }

        [Benchmark]
        public async Task RunAttractor()
        {
            var completion = new TaskCompletionSource();
            var system = ActorSystem.Create();
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
    }
}