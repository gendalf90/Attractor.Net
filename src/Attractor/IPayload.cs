using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IPayload
    {
        Task SerializeAsync(Stream stream, CancellationToken token = default);
    }
}
