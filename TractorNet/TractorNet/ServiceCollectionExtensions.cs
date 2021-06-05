using Microsoft.Extensions.DependencyInjection;
using System;
using TractorNet.Implementation.Factory;

namespace TractorNet
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTractor(this IServiceCollection services, Action<ITractorBuilder> configuration = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var builder = new TractorBuilder(services);

            configuration?.Invoke(builder);

            builder.Build();

            return services;
        }

        public static IServiceCollection ConfigureMemoryMessages(this IServiceCollection services, Action<IMemoryMessagesBuilder> configuration)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var builder = new MemoryMessagesBuilder(services);

            configuration(builder);

            return services;
        }
    }
}
