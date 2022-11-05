using Microsoft.Extensions.DependencyInjection;
using System;

namespace Attractor.Implementation
{
    internal sealed class DefaultBuilder : IDefaultStreamsBuilder
    {
        private readonly IServiceCollection services;

        public DefaultBuilder(IServiceCollection services)
        {
            this.services = services;
        }

        public IServiceCollection Services => services;

        void IStreamBuilder.Decorate<T>(Func<IServiceProvider, T> factory)
        {
            ArgumentNullException.ThrowIfNull(nameof(factory));

            // handlers будут transient, но scope останется
            // регулировать lifetime у зависимостей относительно scope!
            services.AddTransient(provider => new DependencyWrapper<IStreamHandlerDecorator>(factory(provider)));
        }
    }
}
