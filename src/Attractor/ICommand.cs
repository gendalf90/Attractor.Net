using System.Threading.Tasks;

namespace Attractor
{
    internal interface ICommand
    {
        ValueTask ExecuteAsync();
    }
}