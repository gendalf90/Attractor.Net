using System;
using System.Threading.Tasks;

namespace Attractor
{
    public interface ICompletionTokenSource
    {
        IDisposable Attach();

        void Register(Func<ValueTask> callback);
    }
}
