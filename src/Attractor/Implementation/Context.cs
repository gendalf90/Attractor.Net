using System;
using System.Collections.Generic;
using System.Threading;

namespace Attractor.Implementation
{
    public static class Context
    {
        private static readonly AsyncLocal<IContext> local = new();
        private static readonly IDisposable disposing = Disposable.Create(() => local.Value = null);
        
        public static IContext Default()
        {
            return new CachedTypesDecorator(new DictionaryContext());
        }

        public static void Cache<T>()
        {
            CachedTypesDecorator.IndexOf<T>.Initialize();
        }

        public static IContext Current => local.Value;

        internal static IDisposable Use(IContext context)
        {
            local.Value = context;

            return disposing;
        }

        private class DictionaryContext : IContext
        {
            private readonly Dictionary<Type, object> values = new();

            T IContext.Get<T>()
            {
                if (values.TryGetValue(typeof(T), out var result))
                {
                    return result as T;
                }

                return null;
            }

            void IContext.Set<T>(T value)
            {
                if (value == null)
                {
                    values.Remove(typeof(T));
                }
                else
                {
                    values[typeof(T)] = value;
                }
            }
        }

        private class CachedTypesDecorator : IContext
        {
            private static long length;
            
            private readonly object[] values = new object[Interlocked.Read(ref length)];

            private readonly IContext context;

            public CachedTypesDecorator(IContext context)
            {
                this.context = context;
            }

            private bool TryGetFromCached<T>(out T result) where T : class
            {
                result = null;
                
                if (!IndexOf<T>.TryGet(out var index))
                {
                    return false;
                }

                if (index >= values.LongLength)
                {
                    return false;
                }

                result = values[index] as T;
                
                return true;
            }

            T IContext.Get<T>()
            {
                if (TryGetFromCached<T>(out var result))
                {
                    return result;
                }

                return context.Get<T>();
            }

            private bool TrySetToCached<T>(T value) where T : class
            {
                if (!IndexOf<T>.TryGet(out var index))
                {
                    return false;
                }

                if (index >= values.LongLength)
                {
                    return false;
                }

                values[index] = value;

                return true;
            }

            void IContext.Set<T>(T value)
            {
                if (!TrySetToCached(value))
                {
                    context.Set(value);
                }
            }

            public static class IndexOf<T>
            {
                private const int Initialized = 1;
                private const int NotInitialized = 0;
                private const long DefaultIndex = -1;

                private static int state = NotInitialized;
                private static long index = DefaultIndex;

                public static void Initialize()
                {
                    if (Interlocked.CompareExchange(ref state, Initialized, NotInitialized) == NotInitialized)
                    {
                        Interlocked.Exchange(ref index, Interlocked.Increment(ref length) - 1);
                    }
                }

                public static bool TryGet(out long value)
                {
                    value = Interlocked.Read(ref index);

                    return value > DefaultIndex;
                }
            }
        }
    }
}
