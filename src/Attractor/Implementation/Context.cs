using System;
using System.Collections.Generic;

namespace Attractor.Implementation
{
    public static class Context
    {
        public static IContext Default()
        {
            return new DictionaryContext();
        }

        private class DictionaryContext : Dictionary<Type, object>, IContext
        {
            T IContext.Get<T>()
            {
                if (TryGetValue(typeof(T), out var result))
                {
                    return result as T;
                }

                return null;
            }

            void IContext.Set<T>(T value)
            {
                if (value == null)
                {
                    Remove(typeof(T));
                }
                else
                {
                    this[typeof(T)] = value;
                }
            }
        }
    }
}
