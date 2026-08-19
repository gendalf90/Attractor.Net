using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Attractor.Implementation;

public static class Extensions
{
    private static readonly ThreadLocal<IServiceProvider> Provider = new();

    public static IServiceCollection AddActors(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(assembly, nameof(assembly));

        var infos = assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.IsVisible)
            .Select(type => new
            {
                Type = type,
                IsHandler = typeof(IHandler).IsAssignableFrom(type),
                ReceiverTypes = type
                    .GetInterfaces()
                    .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReceiver<>))
                    .SelectMany(t => t.GetGenericArguments())
                    .ToArray()
            })
            .Where(info => info.IsHandler || info.ReceiverTypes.Length > 0)
            .Select(info => new RegisteredActorInfo(info.Type, info.ReceiverTypes));

        foreach (var info in infos)
        {
            services.AddTransient(info.Type);
            services.AddSingleton(info);
        }

        return services;
    }

    private record RegisteredActorInfo(Type Type, Type[] ReceiverTypes);
    
    public static IServiceCollection AddStage(this IServiceCollection services, Action<IRegistry> configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        
        return services.AddSingleton<IStage>(provider => new HostStage(provider, Decorate(configuration, provider)));
    }

    private static Action<IRegistry> Decorate(Action<IRegistry> configuration, IServiceProvider provider)
    {
        return registry =>
        {
            IRegistry providerRegistry = new ProviderRegistryDecorator(registry, provider);
            
            var receiver = typeof(Extensions)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(method => method.Name == nameof(RegisterReceiver));
            
            foreach (var info in provider.GetServices<RegisteredActorInfo>())
            {
                providerRegistry.Register(Address.FromExact(info.Type.Name), Props.From(builder =>
                {
                    var instance = provider.GetRequiredService(info.Type);

                    if (instance is IHandler handler)
                    {
                        builder.OnReceive(handler.OnReceive);
                    }

                    foreach (var type in info.ReceiverTypes)
                    {
                        receiver.MakeGenericMethod(type).Invoke(null, [builder, instance]);
                    }

                    if (instance is IProps props)
                    {
                        props.Configure(builder);
                    }
                }));
            }

            using (UseProvider(provider))
            {
                configuration?.Invoke(providerRegistry);
            }
        };
    }

    private static void RegisterReceiver<T>(IBuilder<IHandler> builder, object instance) where T : class
    {
        var receiver = (IReceiver<T>)instance;
        
        builder.OnReceive<T>(receiver.OnReceive);
    }

    private class ProviderRegistryDecorator(IRegistry registry, IServiceProvider provider) : IRegistry
    {
        void IRegistry.Register(IRouter router, IProps props)
        {
            registry.Register(router, new ProviderPropsDecorator(props, provider));
        }

        void IBuilder<IHandler>.Decorate<T>(Func<T> factory)
        {
            registry.Decorate(factory);
        }
    }

    private class ProviderPropsDecorator(IProps props, IServiceProvider provider) : IProps
    {
        void IProps.Configure(IBuilder<IHandler> builder)
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

    public static void Handle<T>(this IBuilder<IHandler> builder) where T : class, IHandler
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        
        builder.Handle(provider => provider.GetRequiredService<T>());
    }

    public static void Decorate<T>(this IBuilder<IHandler> builder, Func<IServiceProvider, T> factory) where T : class, IHandler, IDecorator<IHandler>
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));
        
        builder.Decorate(Partial(factory, Provider.Value ?? throw new InvalidOperationException()));
    }

    public static void Decorate<T>(this IBuilder<IHandler> builder) where T : class, IHandler, IDecorator<IHandler>
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        
        builder.Decorate(provider => provider.GetRequiredService<T>());
    }

    public static Task Send<T>(this IRef actor, T message, CancellationToken token = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(actor, nameof(actor));
        
        return actor.Send(Message.Value(message), token);
    }

    public static void Fire(this IRef actor, IMessage message, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(actor, nameof(actor));
        
        _ = actor.Send(message, token);
    }

    public static void Fire<T>(this IRef actor, T message, CancellationToken token = default) where T : class
    {
        actor.Fire(Message.Value(message), token);
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
        private readonly CancellationTokenSource cancellation;
        
        public HostStage(IServiceProvider provider, Action<IRegistry> configuration)
        {
            var lifetime = provider.GetService<IHostApplicationLifetime>();

            if (lifetime != null)
            {
                stage = Stage.Run(configuration, lifetime.ApplicationStopping);
            }
            else
            {
                cancellation = new CancellationTokenSource();
                stage = Stage.Run(configuration, cancellation.Token);
            }
        }
        
        public IProxy Play(IAddress address)
        {
            return stage.Play(address);
        }

        public void Dispose()
        {
            using (cancellation)
            {
                cancellation?.Cancel();
            }
        }
    }
}
