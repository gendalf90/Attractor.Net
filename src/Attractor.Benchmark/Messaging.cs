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
        public const int SyncReceiveCount = 1000000;

        public const int AsyncReceiveCount = 100000;

        [Benchmark]
        public async Task SyncReceiveInAttractor()
        {
            var completion = new TaskCompletionSource();
            var system = Implementation.ActorSystem.Create();
            var context = Context.Default();
            var address = Address.FromString("test");
            var message = "test";
            var counter = 0;

            system.Register(Address.FromExact(address), Actor.FromPayload<string>(value => 
            {
                if (value != message)
                {
                    return;
                }
                
                if (++counter == SyncReceiveCount)
                {
                    completion.SetResult();
                }
            }).Register());

            var actor = system.Refer(address);

            context.Set(Payload.From(message));

            for (int i = 0; i < SyncReceiveCount; i++)
            {
                await actor.SendAsync(context);
            }

            await completion.Task;
        }
        
        [Benchmark]
        public async Task SyncReceiveInAkka()
        {
            var completion = new TaskCompletionSource();
            var system = Akka.Actor.ActorSystem.Create("test");
            var message = "test";
            var actor = system.ActorOf(Akka.Actor.Props.Create(() => new SyncReceiveAkkaActor(completion, SyncReceiveCount, message)));
            
            for (int i = 0; i < SyncReceiveCount; i++)
            {
                Akka.Actor.ActorRefImplicitSenderExtensions.Tell(actor, message);
            }

            await completion.Task;
        }

        private class SyncReceiveAkkaActor : Akka.Actor.ReceiveActor
        {
            private int counter = 0;
            
            public SyncReceiveAkkaActor(TaskCompletionSource completion, int count, string message)
            {
                Receive<string>(value =>
                {
                    if (value != message)
                    {
                        return;
                    }
                    
                    if (++counter == count)
                    {
                        completion.SetResult();
                    }
                });
            }
        }

        [Benchmark]
        public async Task SyncReceiveInProto()
        {
            var completion = new TaskCompletionSource();
            var system = new Proto.ActorSystem();
            var counter = 0;
            var message = "test";
            var props = Props.FromFunc(context =>
            {
                if (context.Message is not string str || str != message)
                {
                    return Task.CompletedTask;
                }
                
                if (++counter == SyncReceiveCount)
                {
                    completion.SetResult();
                }

                return Task.CompletedTask;
            });
            var actor = system.Root.Spawn(props);

            for (int i = 0; i < SyncReceiveCount; i++)
            {
                system.Root.Send(actor, message);
            }

            await completion.Task;
        }

        [Benchmark]
        public async Task AsyncReceiveInAttractor()
        {
            var completion = new TaskCompletionSource();
            var system = Implementation.ActorSystem.Create();
            var address = Address.FromString("test");
            var context = Context.Default();
            var message = "test";
            var counter = 0;

            system.Register(Address.FromExact(address), Actor.FromPayload<string>(async (value, _) => 
            {
                if (value != message)
                {
                    return;
                }
                
                await Task.Yield();

                if (++counter == AsyncReceiveCount)
                {
                    completion.SetResult();
                }
            }).Register());

            var actor = system.Refer(address);

            context.Set(Payload.From(message));

            for (int i = 0; i < AsyncReceiveCount; i++)
            {
                await actor.SendAsync(context);
            }

            await completion.Task;
        }
        
        [Benchmark]
        public async Task AsyncReceiveInAkka()
        {
            var completion = new TaskCompletionSource();
            var system = Akka.Actor.ActorSystem.Create("test");
            var message = "test";
            var actor = system.ActorOf(Akka.Actor.Props.Create(() => new AsyncReceiveAkkaActor(completion, AsyncReceiveCount, message)));
            
            for (int i = 0; i < AsyncReceiveCount; i++)
            {
                Akka.Actor.ActorRefImplicitSenderExtensions.Tell(actor, message);
            }

            await completion.Task;
        }

        private class AsyncReceiveAkkaActor : Akka.Actor.ReceiveActor
        {
            private int counter = 0;
            
            public AsyncReceiveAkkaActor(TaskCompletionSource completion, int count, string message)
            {
                ReceiveAsync<string>(async value =>
                {
                    if (value != message)
                    {
                        return;
                    }

                    await Task.Yield();
                    
                    if (++counter == count)
                    {
                        completion.SetResult();
                    }
                });
            }
        }

        [Benchmark]
        public async Task AsyncReceiveInProto()
        {
            var completion = new TaskCompletionSource();
            var system = new Proto.ActorSystem();
            var counter = 0;
            var message = "test";
            var props = Props.FromFunc(async context =>
            {
                if (context.Message is not string str || str != message)
                {
                    return;
                }

                await Task.Yield();
                
                if (++counter == AsyncReceiveCount)
                {
                    completion.SetResult();
                }
            });
            var actor = system.Root.Spawn(props);

            for (int i = 0; i < AsyncReceiveCount; i++)
            {
                system.Root.Send(actor, message);
            }

            await completion.Task;
        }
    }
}