using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;

namespace Attractor.Benchmark
{
    [SimpleJob(launchCount: 1, warmupCount: 3, targetCount: 3, id: "SimpleMessaging")]
    public class SimpleMessaging
    {
        private IHost host;

        [Params(1, 100, 1000, 10000, 100000)]
        public int MessageCount;

        [GlobalSetup]
        public void Setup()
        {
            host = new HostBuilder()
                .ConfigureServices(services =>
                {

                })
                .Build();

            host.Start();
        }

        [Benchmark]
        public int SendWithDifferentAddresses()
        {
            return MessageCount;
        }

        [Benchmark]
        public int SendWithTheSameAddress()
        {
            return MessageCount;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            host.Dispose();
        }
    }
}
