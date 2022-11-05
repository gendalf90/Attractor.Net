using BenchmarkDotNet.Running;

namespace Attractor.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<SimpleMessaging>();
        }
    }
}
