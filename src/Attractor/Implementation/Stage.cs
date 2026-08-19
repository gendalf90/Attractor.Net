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
        private IProps props = Props.Empty;

        IProxy IStage.Play(IAddress address)
        {
            ArgumentNullException.ThrowIfNull(address, nameof(address));

            return new ActorProxy(address, this);
        }

        void IRegistry.Register(IRouter router, IProps props)
        {
            ArgumentNullException.ThrowIfNull(router, nameof(router));
            ArgumentNullException.ThrowIfNull(props, nameof(props));

            registrations.AddFirst(new ActorRegistration(router, props));
        }

        void IBuilder<IHandler>.Decorate<T>(Func<T> factory)
        {
            ArgumentNullException.ThrowIfNull(factory, nameof(factory));

            props = props.With(builder => builder.Decorate(factory));
        }

        private Task Stop(Guid proxyId, IAddress address)
        {
            return strand.Run(async () =>
            {
                using (await Lock(address, cancellation))
                {
                    if (!actors.TryGetValue(address, out var process))
                    {
                        return;
                    }

                    process.Unuse(proxyId, out var disposing);

                    if (disposing)
                    {
                        process.Dispose();
                    }
                }
            });
        }

        private Task Send(Guid proxyId, IAddress address, IMessage message, CancellationToken token)
        {
            return strand.Run(async () =>
            {
                using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation, token);
                
                using (await Lock(address, source.Token))
                {
                    var process = GetOrCreate(proxyId, address);

                    using (UseStage(this))
                    using (Address.UseAddress(address))
                    {
                        await process.Send(message, source.Token);
                    }
                }
            });
        }

        private ActorProcess GetOrCreate(Guid proxyId, IAddress address)
        {
            if (actors.TryGetValue(address, out var process))
            {
                process.Use(proxyId);

                return process;
            }
            
            var registration = registrations.FirstOrDefault(value => value.Router.IsMatch(address));

            if (registration == null)
            {
                throw new ArgumentNullException();
            }

            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var actor = Actor.Run(registration.Props.With(props), source.Token);
            var disposing = Disposable.Create(() =>
            {
                source.Cancel();
                actors.Remove(address);
                source.Dispose();
            });

            process = new ActorProcess(actor, disposing);
            process.Use(proxyId);
            actors.Add(address, process);

            return process;
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
            private readonly HashSet<Guid> usings = new();

            public void Use(Guid id)
            {
                usings.Add(id);
            }
            
            public Task Send(IMessage message, CancellationToken token)
            {
                return actor.Send(message, token);
            }

            public void Unuse(Guid id, out bool needDispose)
            {
                needDispose = usings.Remove(id) && usings.Count == 0;
            }

            public void Dispose()
            {
                disposing.Dispose();
            }
        }

        private class ActorProxy(IAddress address, StageProcess stage) : IProxy
        {
            private readonly Guid id = Guid.NewGuid();
            private readonly Lock sync = new();

            private bool disposed = false;

            Task IRef.Send(IMessage message, CancellationToken token)
            {
                lock (sync)
                {
                    if (disposed)
                    {
                        throw new ObjectDisposedException(nameof(IProxy));
                    }

                    return stage.Send(id, address, message, token);
                }
            }

            void IDisposable.Dispose()
            {
                DisposeInternal();
            }

            ValueTask IAsyncDisposable.DisposeAsync()
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

                    return stage.Stop(id, address);
                }
            }
        }
    }

    public static async Task Shoot(this IStage stage, IAddress address, IMessage message, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(stage, nameof(stage));

        await using var proxy = stage.Play(address);

        await proxy.Send(message, token);
    }

    public static Task Shoot<T>(this IStage stage, IAddress address, T message, CancellationToken token = default) where T : class
    {
        return stage.Shoot(address, Message.Value(message), token);
    }

    public static Task Shoot<T>(this IStage stage, string address, T message, CancellationToken token = default) where T : class
    {
        return stage.Shoot(Address.FromString(address), message, token);
    }

    public static IProxy Play(this IStage stage, string address)
    {
        return stage.Play(Address.FromString(address));
    }
}
