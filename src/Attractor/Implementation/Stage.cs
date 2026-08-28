namespace Attractor.Implementation;

public static class Stage
{
    private static readonly AsyncLocal<IStage> CurrentStage = new();

    public static IStage Current => CurrentStage.Value;
    
    public static ISystem Run(Action<IRegistry> configuration, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
        
        var result = new SystemProcess(token);

        configuration(result);

        return result;
    }
    
    private class SystemProcess(CancellationToken cancellation) : ISystem, IRegistry
    {
        private readonly Strand strand = new();
        private readonly Dictionary<IAddress, ActorProcess> actors = new(Address.EqualityComparer);
        private readonly Dictionary<IAddress, ActorLatch> latches = new(Address.EqualityComparer);
        private readonly LinkedList<ActorRegistration> registrations = new();
        private readonly Lock sync = new();

        private IProps props = Props.Empty;
        private bool disposed = false;

        IProxy IStage.Play(IAddress address)
        {
            ArgumentNullException.ThrowIfNull(address, nameof(address));

            lock (sync)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(ISystem));
                }

                return new ActorProxy(address, this);
            }
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
                using (await Lock(address))
                {
                    if (!actors.TryGetValue(address, out var process))
                    {
                        return;
                    }

                    process.Unuse(proxyId, out var disposing);

                    if (disposing)
                    {
                        await process.DisposeAsync();
                    }
                }
            });
        }

        private Task Send(Guid proxyId, IAddress address, IMessage message, CancellationToken token)
        {
            lock (sync)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(ISystem));
                }

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

        private Task<IDisposable> Lock(IAddress address, CancellationToken token = default)
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

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return ValueTask.CompletedTask;
                }

                disposed = true;

                return new ValueTask(strand.Run(async () =>
                {
                    foreach (var address in actors.Keys.ToList())
                    {
                        using (await Lock(address))
                        {
                            if (actors.TryGetValue(address, out var process))
                            {
                                await process.DisposeAsync();
                            }
                        }
                    }
                }));
            }
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

        private class ActorProcess(IActor actor, IDisposable disposing) : IRef, IAsyncDisposable
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

            public async ValueTask DisposeAsync()
            {
                using (disposing)
                {
                    await actor.DisposeAsync();
                }
            }
        }

        private class ActorProxy(IAddress address, SystemProcess system) : IProxy
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

                    return system.Send(id, address, message, token);
                }
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return new ValueTask(DisposeInternal());
            }

            void IDisposable.Dispose()
            {
                DisposeInternal();
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

                    return system.Stop(id, address);
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
