using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Attractor.Implementation
{
    internal static class DynamicType
    {
        private static int typeKeyNumber;

        private readonly static ModuleBuilder moduleBuilder = AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName("DynamicAssembly"), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("DynamicModule");

        public static Type Create()
        {
            return moduleBuilder
                .DefineType($"DynamicType_<{Interlocked.Increment(ref typeKeyNumber)}>")
                .CreateType();
        }

        public static void Invoke(IDynamicExecutor executor)
        {
            typeof(IDynamicExecutor)
                .GetMethod(nameof(IDynamicExecutor.Invoke))
                .MakeGenericMethod(Create())
                .Invoke(executor, Array.Empty<object>());
        }
    }
}
