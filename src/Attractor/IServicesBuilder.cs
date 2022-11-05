using Microsoft.Extensions.DependencyInjection;

namespace Attractor
{
    public interface IServicesBuilder
    {
        IServiceCollection Services { get; }
    }
}
