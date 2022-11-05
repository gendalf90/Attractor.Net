using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class DefaultSystem : ISystem
    {
        private readonly ConcurrentDictionary<IAddress, Init> streams = new(AddressEqualityComparer.Default);

        private readonly CancellationToken cancellation;
        private readonly IServiceProvider services;

        public DefaultSystem(IServiceProvider services, CancellationToken cancellation)
        {
            this.services = services;
            this.cancellation = cancellation;
        }

        public async ValueTask<IRef> GetAsync(IAddress address, CancellationToken token)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            cancellation.ThrowIfCancellationRequested();
            token.ThrowIfCancellationRequested();

            return await GetOrCreate(address.Clone()).StartAndGetAsync(token);
        }

        private Init GetOrCreate(IAddress address)
        {
            return streams.GetOrAdd(address, key =>
            {
                var refDisposing = Disposable.Create(() =>
                {
                    streams.TryRemove(key, out _);
                });

                return new Init(this, key, refDisposing);
            });
        }

        private class Init
        {
            private const int InitialState = 0;
            private const int StartingState = 1;

            private readonly TaskCompletionSource<IRef> initCompletion = new();

            private readonly IAddress address;
            private readonly DefaultSystem system;
            private readonly IDisposable refDisposing;

            private int state = InitialState;

            public Init(DefaultSystem system, IAddress address, IDisposable refDisposing)
            {
                this.address = address;
                this.system = system;
                this.refDisposing = refDisposing;
            }

            public async Task<IRef> StartAndGetAsync(CancellationToken token = default)
            {
                if (Interlocked.CompareExchange(ref state, StartingState, InitialState) != InitialState)
                {
                    return await initCompletion.Task;
                }

                IScopedStreamHandler stream = null;
                Exception error = null;
                Ref result = null;

                var completionSource = new CompletionTokenSource();
                var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token, system.cancellation);

                try
                {
                    stream = CreateStream(address);

                    result = new Ref(address, stream, cancellationSource, completionSource);

                    var context = Context.Default();

                    context.Set<IRef>(result); // можем отменять т.к. еще не подписаны на cancel и complete
                    context.Set<ICompletionTokenSource>(completionSource);

                    await stream.OnStartAsync(context);

                    initCompletion.SetResult(result);
                }
                catch (Exception e)
                {
                    error = e;

                    cancellationSource.Cancel();
                }
                finally
                {
                    completionSource.Register(async () =>
                    {
                        if (stream != null)
                        {
                            await stream.DisposeAsync();
                        }
                    });
                    completionSource
                        .WaitAsync()
                        .GetAwaiter()
                        .OnCompleted(() =>
                        {
                            cancellationSource.Dispose();
                            refDisposing.Dispose();

                            if (error != null)
                            {
                                initCompletion.SetException(error);
                            }
                        });
                    cancellationSource.Token.Register(completionSource.Dispose);
                }

                return await initCompletion.Task;
            }

            private IScopedStreamHandler CreateStream(IAddress address)
            {
                foreach (var factory in system.services.GetServices<IStreamFactory>())
                {
                    var policy = factory.CreateAddressPolicy();

                    if (policy.IsMatch(address))
                    {
                        return factory.CreateStream();
                    }
                }

                throw new InvalidOperationException();
            }
        }

        private class Ref : IRef
        {
            private readonly CancellationTokenSource streamCancellation;
            private readonly CompletionTokenSource streamCompletion;
            private readonly IAddress address;
            private readonly IStreamHandler handler;

            public Ref(
                IAddress address, 
                IStreamHandler handler,
                CancellationTokenSource streamCancellation,
                CompletionTokenSource streamCompletion)
            {
                this.address = address;
                this.handler = handler;
                this.streamCancellation = streamCancellation;
                this.streamCompletion = streamCompletion;
            }

            public void Cancel()
            {
                try
                {
                    streamCancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }

            public CancellationToken GetToken()
            {
                try
                {
                    return streamCancellation.Token;
                }
                catch (ObjectDisposedException)
                {
                    return new CancellationToken(true);
                }
            }

            public async ValueTask<IRequest> SendAsync(IPayload payload, Action<IContext> configuration, CancellationToken token)
            {
                if (payload == null)
                {
                    throw new ArgumentNullException(nameof(payload));
                }

                token.ThrowIfCancellationRequested();

                using (UseRequest(payload.Clone(), token, out var request))
                {
                    var context = Context.Default();

                    configuration?.Invoke(context);

                    context.Set<IRef>(this);
                    context.Set<IRequest>(request);
                    context.Set<ICompletionTokenSource>(request);
                    
                    await handler.OnReceiveAsync(context);
                    
                    return request;
                }
            }

            private IDisposable UseRequest(IPayload payload, CancellationToken token, out Request request)
            {
                return request = StartRequest(payload, streamCompletion.Attach(), token);
            }

            private Request StartRequest(IPayload payload, IDisposable streamLockDisposing, CancellationToken token)
            {
                var requestCompletion = new CompletionTokenSource();
                var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(streamCancellation.Token, token);
                var requestDisposing = requestCompletion.Attach();

                requestCancellation.Token.Register(requestCompletion.Dispose);
                requestCompletion
                    .WaitAsync()
                    .GetAwaiter()
                    .OnCompleted(() =>
                    {
                        streamLockDisposing.Dispose();
                        requestCancellation.Dispose();
                    });

                return new Request(payload, requestDisposing, requestCompletion, requestCancellation);
            }

            public Task WaitAsync(CancellationToken token = default)
            {
                return streamCompletion.WaitAsync(token);
            }

            public IAddress Clone()
            {
                return address.Clone();
            }

            public void Accept(IVisitor visitor)
            {
                address.Accept(visitor);
            }

            public IEquatable<IAddress> GetEquatable()
            {
                return address.GetEquatable();
            }
        }

        private class Request : IRequest, ICompletionTokenSource, IDisposable
        {
            private readonly CompletionTokenSource completion;
            private readonly CancellationTokenSource cancellation;
            private readonly IPayload payload;
            private readonly IDisposable disposing;

            public Request(IPayload payload, IDisposable disposing, CompletionTokenSource completion, CancellationTokenSource cancellation)
            {
                this.payload = payload;
                this.disposing = disposing;
                this.completion = completion;
                this.cancellation = cancellation;
            }

            public IPayload Clone()
            {
                return payload.Clone();
            }

            public IDisposable Attach()
            {
                return completion.Attach();
            }

            public void Register(Func<ValueTask> callback)
            {
                completion.Register(callback);
            }

            public void Cancel()
            {
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }

            public CancellationToken GetToken()
            {
                try
                {
                    return cancellation.Token;
                }
                catch (ObjectDisposedException)
                {
                    return new CancellationToken(true);
                }
            }

            public Task WaitAsync(CancellationToken token = default)
            {
                return completion.WaitAsync(token);
            }

            public void Dispose()
            {
                disposing.Dispose();
                completion.Dispose();
            }

            public void Accept(IVisitor visitor)
            {
                payload.Accept(visitor);
            }
        }
    }
}
