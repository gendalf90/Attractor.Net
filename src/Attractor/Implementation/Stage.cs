using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

public static class Stage
{
    private static readonly AsyncLocal<IStage> CurrentStage = new();

    public static IStage Current => CurrentStage.Value;
    
    public static IStage Run(Action<IRegistry> configuration, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
        
        var result = new StageProcess(token);

        configuration(result);

        return result;
    }
    
    private class StageProcess(CancellationToken cancellation) : IStage, IRegistry
    {
        private readonly Strand strand = new();
        private readonly Dictionary<IAddress, ActorProcess> actors = new(Address.EqualityComparer);
        private readonly Dictionary<IAddress, ActorLatch> latches = new(Address.EqualityComparer);
        private readonly LinkedList<ActorRegistration> registrations = new();

        public IProxy Play(IAddress address)
        {
            ArgumentNullException.ThrowIfNull(address, nameof(address));

            return new ActorProxy(address, this);
        }

        public void Register(IRouter router, IProps props)
        {
            ArgumentNullException.ThrowIfNull(router, nameof(router));
            ArgumentNullException.ThrowIfNull(props, nameof(props));

            registrations.AddFirst(new ActorRegistration(router, props));
        }

        private Task Run(IAddress address)
        {
            return strand.Run(async () =>
            {
                using (await Lock(address, cancellation))
                {
                    if (actors.TryGetValue(address, out var process))
                    {
                        process.Use();

                        return;
                    }

                    var registration = registrations.FirstOrDefault(value => value.Router.IsMatch(address));

                    if (registration == null)
                    {
                        throw new ArgumentNullException();
                    }

                    var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    var actor = Actor.Run(registration.Props.With(Props.From(builder => 
                    {
                        builder.Use<IStage>(this);
                        builder.Use(address);
                    })), source.Token);
                    var disposing = Disposable.Create(() =>
                    {
                        source.Cancel();
                        actors.Remove(address);
                        source.Dispose();
                    });

                    process = new ActorProcess(actor, disposing);

                    process.Use();

                    actors.Add(address, process);
                }
            });
        }

        private Task Stop(IAddress address)
        {
            return strand.Run(async () =>
            {
                using (await Lock(address, cancellation))
                {
                    if (!actors.TryGetValue(address, out var process))
                    {
                        return;
                    }

                    process.Unuse(out var disposing);

                    if (disposing)
                    {
                        process.Dispose();
                    }
                }
            });
        }

        private Task Send(IAddress address, IMessage message, CancellationToken token)
        {
            return strand.Run(async () =>
            {
                using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation, token);
                
                using (await Lock(address, source.Token))
                {
                    if (!actors.TryGetValue(address, out var process))
                    {
                        throw new ArgumentNullException();
                    }

                    using (UseStage(this))
                    using (Address.UseAddress(address))
                    {
                        await process.Send(message, source.Token);
                    }
                }
            });
        }

        private Task<IDisposable> Lock(IAddress address, CancellationToken token)
        {
            if (!latches.TryGetValue(address, out var result))
            {
                result = new ActorLatch(strand, Disposable.Create(() => latches.Remove(address)));
                latches.Add(address, result);
            }

            return result.Use(token);
        }

        private static IDisposable UseStage(IStage stage)
        {
            var current = CurrentStage.Value;

            CurrentStage.Value = stage;

            return Disposable.Create(() => CurrentStage.Value = current);
        }

        private record ActorRegistration(IRouter Router, IProps Props);

        private class ActorLatch(Strand strand, IDisposable disposing)
        {
            private readonly Latch latch = new(strand);

            private long counter = 0;
            
            public async Task<IDisposable> Use(CancellationToken token)
            {
                counter++;

                try
                {
                    var disposer = await latch.Use(token);

                    return disposer.With(Disposable.Create(Dispose));
                }
                catch
                {
                    Dispose();

                    throw;
                }
            }

            private void Dispose()
            {
                if (--counter == 0)
                {
                    disposing.Dispose();
                }
            }
        }

        private class ActorProcess(IActor actor, IDisposable disposing) : IRef, IDisposable
        {
            private long counter = 0;

            public void Use()
            {
                counter++;
            }
            
            public Task Send(IMessage message, CancellationToken token)
            {
                return actor.Send(message, token);
            }

            public void Unuse(out bool noReferences)
            {
                noReferences = --counter == 0;
            }

            public void Dispose()
            {
                disposing.Dispose();
            }
        }

        private class ActorProxy(IAddress address, StageProcess stage) : IProxy
        {
            private readonly Lock sync = new();

            private bool started = false;
            private bool disposed = false;
            
            public Task Send(IMessage message, CancellationToken token)
            {
                lock (sync)
                {
                    if (disposed)
                    {
                        throw new ObjectDisposedException(nameof(IProxy));
                    }

                    if (started)
                    {
                        return stage.Send(address, message, token);
                    }

                    started = true;

                    return RunAndSend(message, token);
                }
            }

            private async Task RunAndSend(IMessage message, CancellationToken token)
            {
                await stage.Run(address);
                await stage.Send(address, message, token);
            }

            public void Dispose()
            {
                DisposeInternal();
            }

            public ValueTask DisposeAsync()
            {
                return new ValueTask(DisposeInternal());
            }

            private Task DisposeInternal()
            {
                lock (sync)
                {
                    if (disposed)
                    {
                        return Task.CompletedTask;
                    }

                    disposed = true;

                    if (!started)
                    {
                        return Task.CompletedTask;
                    }

                    return stage.Stop(address);
                }
            }
        }
    }
}
