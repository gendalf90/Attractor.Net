using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class StreamAddressBuilder<Tkey> : IAddressStreamBuilder
    {
        private readonly IServiceCollection services;

        public StreamAddressBuilder(IServiceCollection services, Func<IServiceProvider, IAddressPolicy> addressPolicyCreator)
        {
            this.services = services;

            services.AddSingleton<IStreamFactory, StreamFactory>();

            services.AddTransient(provider => new StreamFactoryDependencyWrapper<IAddressPolicy>(addressPolicyCreator(provider)));

            // handlers будут transient, но scope останется
            // регулировать lifetime у зависимостей относительно scope!
            services.AddTransient(provider =>
            {
                IStreamHandler result = new EmptyHandler();

                var defaultDecorators = new List<IStreamHandlerDecorator>
                {
                    new ProviderHandler(provider)
                };
                var currentDecorators = provider.GetServices<StreamBuilderDependencyWrapper<IStreamHandlerDecorator>>();
                var commonDecorators = provider.GetServices<DependencyWrapper<IStreamHandlerDecorator>>();

                foreach (var decorator in currentDecorators.Reverse())
                {
                    decorator.Value.Decorate(result);

                    result = decorator.Value;
                }

                foreach (var decorator in commonDecorators.Reverse())
                {
                    decorator.Value.Decorate(result);

                    result = decorator.Value;
                }

                foreach (var decorator in defaultDecorators)
                {
                    decorator.Decorate(result);

                    result = decorator;
                }

                return new StreamFactoryDependencyWrapper<IStreamHandler>(result);
            });
        }

        public IServiceCollection Services => services;

        void IStreamBuilder.Decorate<T>(Func<IServiceProvider, T> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            // handlers будут transient, но scope останется
            // регулировать lifetime у зависимостей относительно scope!
            services.AddTransient(provider => new StreamBuilderDependencyWrapper<IStreamHandlerDecorator>(factory(provider)));
        }

        private class StreamFactory : IStreamFactory
        {
            private readonly IServiceProvider provider;

            public StreamFactory(IServiceProvider provider)
            {
                this.provider = provider;
            }

            public IAddressPolicy CreateAddressPolicy()
            {
                var wrapper = provider.GetRequiredService<StreamFactoryDependencyWrapper<IAddressPolicy>>();

                return wrapper.Value;
            }

            public IScopedStreamHandler CreateStream()
            {
                var scope = provider.CreateAsyncScope();
                var handler = provider.GetRequiredService<StreamFactoryDependencyWrapper<IStreamHandler>>();
                var result = new ScopedHandler(scope);

                result.Decorate(handler.Value);

                return result;
            }
        }

        private class ScopedHandler : BaseHandlerDecorator, IScopedStreamHandler
        {
            private readonly AsyncServiceScope scope;

            public ScopedHandler(AsyncServiceScope scope)
            {
                this.scope = scope;
            }

            public void Dispose()
            {
                scope.Dispose();
            }

            public ValueTask DisposeAsync()
            {
                return scope.DisposeAsync();
            }
        }

        private class StreamBuilderDependencyWrapper<TValue> : DependencyWrapper<TValue>
        {
            public StreamBuilderDependencyWrapper(TValue value) : base(value) { }
        }

        private class StreamFactoryDependencyWrapper<TValue> : DependencyWrapper<TValue>
        {
            public StreamFactoryDependencyWrapper(TValue value) : base(value) { }
        }
    }
}
