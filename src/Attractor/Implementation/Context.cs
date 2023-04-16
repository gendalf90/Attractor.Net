using System;
using System.Collections.Generic;

namespace Attractor.Implementation
{
    public static class Context
    {
        public static IContext Default()
        {
            return new CachedTypesDecorator(new DictionaryContext());
        }

        private class DictionaryContext : IContext
        {
            private readonly Dictionary<Type, object> values;

            public DictionaryContext(Dictionary<Type, object> values)
            {
                this.values = values;
            }

            public DictionaryContext() : this(new())
            {
            }

            public IContext Clone()
            {
                return new DictionaryContext(new(values));
            }

            public T Get<T>() where T : class
            {
                if (values.TryGetValue(typeof(T), out var result))
                {
                    return result as T;
                }

                return null;
            }

            public void Set<T>(T value) where T : class
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
            private static readonly Action<int>[] TypeInits =
            {
                IndexOf<IActorProcess>.Set
            };
            
            static CachedTypesDecorator()
            {
                for (int i = 0; i < TypeInits.Length; i++)
                {
                    TypeInits[i](i);
                }
            }
            
            private readonly object[] values = new object[TypeInits.Length];

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

                result = values[index] as T;
                
                return true;
            }

            public T Get<T>() where T : class
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

                values[index] = value;

                return true;
            }

            public void Set<T>(T value) where T : class
            {
                if (!TrySetToCached(value))
                {
                    context.Set(value);
                }
            }

            public IContext Clone()
            {
                var result = new CachedTypesDecorator(context.Clone());

                for (int i = 0; i < values.Length; i++)
                {
                    result.values[i] = values[i];
                }

                return result;
            }

            private static class IndexOf<T>
            {
                private const int NotInitialized = -1;
                
                private static int index = NotInitialized;

                public static void Set(int value)
                {
                    index = value;
                }

                public static bool TryGet(out int value)
                {
                    value = index;

                    return index > NotInitialized;
                }
            }
        }
    }
}
