#if NETSTANDARD2_0

namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}

namespace System.Threading.Tasks
{
    internal class TaskCompletionSource
    {
        private readonly TaskCompletionSource<int> source = new TaskCompletionSource<int>();

        public void SetResult()
        {
            source.SetResult(0);
        }

        public Task Task => source.Task;
    }
}

namespace System.Reflection.Emit
{
    internal static class TypeBuilderExtensions
    {
        public static Type CreateType(this TypeBuilder builder)
        {
            return builder.CreateTypeInfo().AsType();
        }
    }
}

#endif
