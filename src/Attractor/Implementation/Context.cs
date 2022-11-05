using System;
using System.Collections.Generic;

namespace Attractor.Implementation
{
    internal static class Context
    {
        public static IContext Default()
        {
            return new DefaultFeaturesDecorator(new DictionaryContext());
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

        private class DefaultFeaturesDecorator : IContext
        {
            private readonly (Type Key, object Value)[] features = new (Type Key, object Value)[]
            {
                (typeof(IRef), null),
                (typeof(IRequest), null),
                (typeof(IServiceProvider), null),
                (typeof(IAsyncToken), null),
                (typeof(ICompletionTokenSource), null)
            };

            private readonly IContext context;

            public DefaultFeaturesDecorator(IContext context)
            {
                this.context = context;
            }

            private bool TryGetFromCommon<T>(out T result) where T : class
            {
                result = null;
                
                for (int i = 0; i < features.Length; i++)
                {
                    if (features[i].Key == typeof(T))
                    {
                        result = features[i].Value as T;

                        return true;
                    }
                }

                return false;
            }

            public T Get<T>() where T : class
            {
                if (TryGetFromCommon<T>(out var result))
                {
                    return result;
                }

                return context.Get<T>();
            }

            private bool TrySetToCommon<T>(T value) where T : class
            {
                for (int i = 0; i < features.Length; i++)
                {
                    if (features[i].Key == typeof(T))
                    {
                        features[i].Value = value;

                        return true;
                    }
                }

                return false;
            }

            public void Set<T>(T value) where T : class
            {
                if (!TrySetToCommon(value))
                {
                    context.Set(value);
                }
            }

            public IContext Clone()
            {
                var result = new DefaultFeaturesDecorator(context.Clone());

                for (int i = 0; i < features.Length; i++)
                {
                    result.features[i].Value = features[i].Value;
                }

                return result;
            }
        }
    }
}
