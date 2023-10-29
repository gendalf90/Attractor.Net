using System;

namespace Attractor.Implementation
{
    internal static class Disposable
    {
        public static IDisposable FromStrategy(Action strategy)
        {
            ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

            return new Disposing(strategy);
        }

        private record Disposing(Action Strategy) : IDisposable
        {
            void IDisposable.Dispose()
            {
                Strategy();
            }
        }
    }
}