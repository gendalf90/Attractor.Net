using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Attractor.Implementation;

public static class Extensions
{
    private static readonly ThreadLocal<IServiceProvider> Provider = new();
    
    public static IServiceCollection AddStage(this IServiceCollection services, Action<IRegistry> configuration)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
        
        return services.AddSingleton<IStage>(provider => new HostStage(provider, Decorate(configuration, provider)));
    }

    private static Action<IRegistry> Decorate(Action<IRegistry> configuration, IServiceProvider provider)
    {
        return registry =>
        {
            configuration(new ProviderRegistryDecorator(registry, provider));
        };
    }

    private class ProviderRegistryDecorator(IRegistry registry, IServiceProvider provider) : IRegistry
    {
        public void Register(IRouter router, IProps props)
        {
            registry.Register(router, new ProviderPropsDecorator(props, provider));
        }
    }

    private class ProviderPropsDecorator(IProps props, IServiceProvider provider) : IProps
    {
        public void Configure(IBuilder<IHandler> builder)
        {
            using (UseProvider(provider))
            {
                props.Configure(builder);
            }
        }
    }

    private static IDisposable UseProvider(IServiceProvider provider)
    {
        var current = Provider.Value;

        Provider.Value = provider;

        return Disposable.Create(() =>
        {
            Provider.Value = current; 
        });
    }

    public static void Handle<T>(this IBuilder<IHandler> builder, Func<IServiceProvider, T> factory) where T : class, IHandler
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));
        
        builder.Decorate(provider => new HandlerDecorator(factory(provider)));
    }

    public static void Decorate<T>(this IBuilder<IHandler> builder, Func<IServiceProvider, T> factory) where T : class, IHandler, IDecorator<IHandler>
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));
        
        builder.Decorate(Partial(factory, Provider.Value ?? throw new InvalidOperationException()));
    }

    public static Task Send<T>(this IRef actor, T message, CancellationToken token = default) where T : class
    {
        return actor.Send(Message.Value(message), token);
    }

    private static Func<TResult> Partial<T, TResult>(Func<T, TResult> func, T value)
    {
        return () => func(value);
    }

    private class HandlerDecorator(IHandler handler) : IHandler, IDecorator<IHandler>
    {
        private IHandler decoratee;

        void IDecorator<IHandler>.Decorate(IHandler value)
        {
            decoratee = value;
        }

        async Task IHandler.OnReceive(IContext context, CancellationToken token)
        {
            await decoratee.OnReceive(context, token);
            await handler.OnReceive(context, token);
        }
    }

    private class HostStage : IStage
    {
        private readonly IStage stage;
        private readonly IServiceProvider provider;
        private readonly CancellationTokenSource cancellation;
        
        public HostStage(IServiceProvider provider, Action<IRegistry> configuration)
        {
            this.provider = provider;
            
            var lifetime = provider.GetService<IHostApplicationLifetime>();

            cancellation = lifetime == null
                ? new CancellationTokenSource()
                : CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);

            stage = Stage.Run(configuration, cancellation.Token);
        }
        
        public IProxy Play(IAddress address)
        {
            return stage.Play(address);
        }

        public void Dispose()
        {
            using (cancellation)
            {
                cancellation.Cancel();
            }
        }
    }
}
