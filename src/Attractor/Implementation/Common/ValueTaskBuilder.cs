using System.Threading.Tasks;

namespace Attractor.Implementation.Common
{
    internal static class ValueTaskBuilder
    {
        public static ValueTask CompletedTask
        {
            get
            {
#if NETSTANDARD2_0
                return new ValueTask(Task.CompletedTask);
#else
                return ValueTask.CompletedTask;
#endif
            }
        }

        public static ValueTask<T> FromResult<T>(T result)
        {
#if NETSTANDARD2_0
            return new ValueTask<T>(Task.FromResult(result));
#else
            return ValueTask.FromResult(result);
#endif
        }
    }
}
