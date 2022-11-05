using Attractor.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public static class RegistrationExtensions
    {
        public static void AddSystem(this IServiceCollection services, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));

            services.AddSingleton<ISystem>(provider => new DefaultSystem(provider, token));
        }

        public static void ConfigureStreams(
            this IServiceCollection services,
            Action<IDefaultStreamsBuilder> configuration = null)
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));

            var builder = new DefaultBuilder(services);

            configuration?.Invoke(builder);
        }

        public static void MapStream<TAddressPolicy>(
            this IServiceCollection services,
            Func<IServiceProvider, TAddressPolicy> addressPolicyFactory,
            Action<IAddressStreamBuilder> configuration = null)
            where TAddressPolicy : class, IAddressPolicy
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));
            ArgumentNullException.ThrowIfNull(addressPolicyFactory, nameof(addressPolicyFactory));

            //var builder = (IAddressStreamBuilder)Activator.CreateInstance(typeof(StreamAddressBuilder<>).MakeGenericType(DynamicType.Create()), services, addressPolicyFactory);

            //configuration?.Invoke(builder);

            var configurator = new AddressStreamBuilderConfigurator(services, addressPolicyFactory, configuration);

            configurator.Configure();
        }

        public static void MapStream(
            this IServiceCollection services,
            Func<IAddress, bool> addressPolicy,
            Action<IAddressStreamBuilder> configuration = null)
        {
            ArgumentNullException.ThrowIfNull(addressPolicy, nameof(addressPolicy));

            services.MapStream(_ => new StrategyAddressPolicy(addressPolicy), configuration);
        }

        public static void MapStream(
            this IServiceCollection services,
            IAddressPolicy addressPolicy,
            Action<IAddressStreamBuilder> configuration = null)
        {
            ArgumentNullException.ThrowIfNull(addressPolicy, nameof(addressPolicy));

            services.MapStream(_ => addressPolicy, configuration);
        }

        public static void RunAsyncQueue(this IStreamBuilder builder)
        {
            builder.Decorate<AsyncRequestHandler>();
            builder.Decorate<AsyncQueueHandler>();
            builder.Decorate<AsyncRequestHandler>();
        }

        public static void Decorate<T>(this IStreamBuilder builder) where T : class, IStreamHandlerDecorator
        {
            // handlers будут transient, но scope останется
            // регулировать lifetime у зависимостей относительно scope!
            builder.Services.TryAddTransient<T>();

            builder.Decorate(provider => provider.GetRequiredService<T>());
        }

        public static void Decorate(this IStreamBuilder builder, IStreamHandlerDecorator decorator)
        {
            builder.Decorate(_ => decorator);
        }

        public static void Decorate(
            this IStreamBuilder builder, 
            Func<Func<IContext, ValueTask>, IContext, ValueTask> onStart = null, 
            Func<Func<IContext, ValueTask>, IContext, ValueTask> onReceive = null)
        {
            builder.Decorate(_ => new StrategyHandlerDecorator(onStart, onReceive));
        }

        public static void Chain<T>(this IStreamBuilder builder, Func<IServiceProvider, T> factory) where T : class, IStreamHandler
        {
            builder.Decorate(provider => new ChainHandler(factory(provider)));
        }

        public static void Chain<T>(this IStreamBuilder builder) where T : class, IStreamHandler
        {
            builder.Services.TryAddTransient<T>();

            builder.Chain(provider => provider.GetRequiredService<T>());
        }

        public static void Chain(this IStreamBuilder builder, IStreamHandler handler)
        {
            builder.Chain(_ => handler);
        }

        public static void Chain(
            this IStreamBuilder builder,
            Func<IContext, ValueTask> onStart = null,
            Func<IContext, ValueTask> onReceive = null)
        {
            builder.Chain(_ => new StrategyHandler(onStart, onReceive));
        }

        public static void Chain(
            this IStreamBuilder builder,
            Action<IContext> onStart = null,
            Action<IContext> onReceive = null)
        {
            builder.Chain(_ => new StrategyHandler(
                onStart == null ? null : context =>
                {
                    onStart(context);

                    return ValueTask.CompletedTask;
                },
                onReceive == null ? null : context =>
                {
                    onReceive(context);

                    return ValueTask.CompletedTask;
                }));
        }

        public static void RequestPool(this IStreamBuilder builder, int capacity, TimeSpan? timeout = null, bool shared = false)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            var type = DynamicType.Create();

            if (shared)
            {
                builder.Services.AddSingleton(type, _ => new BoundedPool(capacity, timeout));
            }
            else
            {
                builder.Services.AddScoped(type, _ => new BoundedPool(capacity, timeout));
            }

            builder.Decorate(provider => new RequestPoolHandler((IPool)provider.GetRequiredService(type)));
        }

        public static void StreamPool(this IStreamBuilder builder, int capacity, TimeSpan? timeout = null)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            var type = DynamicType.Create();

            builder.Services.AddSingleton(type, _ => new BoundedPool(capacity, timeout));

            builder.Decorate(provider => new StreamPoolHandler((IPool)provider.GetRequiredService(type)));
        }

        public static void RequestLimit(this IStreamBuilder builder, int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            builder.Decorate(_ => new LimitedRequestHandler(count));
        }

        public static void StreamTTL(this IStreamBuilder builder, TimeSpan ttl)
        {
            builder.Decorate(_ => new StreamTTLHandler(ttl));
        }

        public static void RequestTTL(this IStreamBuilder builder, TimeSpan ttl)
        {
            builder.Decorate(_ => new RequestTTLHandler(ttl));
        }

        public static void ReceivingTimeout(this IStreamBuilder builder, TimeSpan timeout)
        {
            builder.Decorate(_ => new ReceivingTimeoutHandler(timeout));
        }
    }
}
