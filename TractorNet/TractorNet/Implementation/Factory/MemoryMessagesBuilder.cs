using Microsoft.Extensions.DependencyInjection;
using System;
using TractorNet.Implementation.Message;

namespace TractorNet.Implementation.Factory
{
    internal sealed class MemoryMessagesBuilder : IMemoryMessagesBuilder
    {
        private readonly IServiceCollection services;

        public MemoryMessagesBuilder(IServiceCollection services)
        {
            this.services = services;
        }

        public void UseReadTrottleTime(TimeSpan time)
        {
            services.Configure<MemoryMailboxSettings>(settings =>
            {
                settings.ReadTrottleTime = time;
            });
        }
    }
}
