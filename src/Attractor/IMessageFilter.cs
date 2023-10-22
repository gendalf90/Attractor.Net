using System.Collections;

namespace Attractor
{
    public interface IMessageFilter
    {
        bool IsMatch(IList context);
    }
}