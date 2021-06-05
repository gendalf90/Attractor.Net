using System;
using System.Threading;

namespace TractorNet.Implementation.Common
{
    internal static class CancellationTokenExtensions
    {
        public static IAsyncDisposable WithDelay(this CancellationToken token, TimeSpan delay, out CancellationToken result)
        {
            var delaySource = new CancellationTokenSource(delay);
            var resultSource = CancellationTokenSource.CreateLinkedTokenSource(delaySource.Token, token);

            result = resultSource.Token;

            return new StrategyDisposable(() =>
            {
                delaySource.Dispose();
                resultSource.Dispose();
            });
        }
    }
}
