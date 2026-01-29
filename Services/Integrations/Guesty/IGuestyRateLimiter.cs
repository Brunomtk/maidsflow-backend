using System.Threading;
using System.Threading.Tasks;

namespace Services.Integrations.Guesty
{
    public interface IGuestyRateLimiter
    {
        ValueTask AcquireAsync(CancellationToken cancellationToken = default);
    }
}
