using System;
using System.Reflection;
using System.Reflection.Emit;

namespace TractorNet.Implementation.Factory
{
    internal static class ActorTypeKeyCreator
    {
        private static int actorTypeKeyNumber;

        private readonly static ModuleBuilder actorModuleBuilder = AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName("ActorAssembly"), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("ActorModule");

        public static Type Create()
        {
            return actorModuleBuilder
                .DefineType($"ActorTypeKey_<{++actorTypeKeyNumber}>")
                .CreateType();
        }
    }
}
