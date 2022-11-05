using System.Threading.Tasks;

namespace Attractor
{
    public interface IStreamHandler
    {
        ValueTask OnStartAsync(IContext context);

        ValueTask OnReceiveAsync(IContext context);
    }
}
