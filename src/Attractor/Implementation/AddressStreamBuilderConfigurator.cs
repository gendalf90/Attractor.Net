using Microsoft.Extensions.DependencyInjection;
using System;

namespace Attractor.Implementation
{
    internal sealed class AddressStreamBuilderConfigurator : IDynamicExecutor
    {
        private readonly IServiceCollection services;
        private readonly Func<IServiceProvider, IAddressPolicy> addressPolicyCreator;
        private readonly Action<IAddressStreamBuilder> configuration;

        public AddressStreamBuilderConfigurator(
            IServiceCollection services,
            Func<IServiceProvider, IAddressPolicy> addressPolicyCreator,
            Action<IAddressStreamBuilder> configuration)
        {
            this.services = services;
            this.addressPolicyCreator = addressPolicyCreator;
            this.configuration = configuration;
        }

        void IDynamicExecutor.Invoke<T>()
        {
            var result = new StreamAddressBuilder<T>(services, addressPolicyCreator);

            configuration?.Invoke(result);
        }

        public void Configure()
        {
            DynamicType.Invoke(this);
        }
    }
}
