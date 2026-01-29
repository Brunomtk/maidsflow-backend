using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Core.Exceptions;

namespace Services.Integrations.Guesty
{
    /// <summary>
    /// Guesty Booking Engine API is heavily rate-limited.
    /// This limiter targets ~5 requests/second (token bucket) with a bounded queue.
    /// </summary>
    public sealed class GuestyRateLimiter : IGuestyRateLimiter, IAsyncDisposable
    {
        private readonly TokenBucketRateLimiter _limiter;

        public GuestyRateLimiter()
        {
            _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = 5,
                TokensPerPeriod = 5,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2048
            });
        }

        public async ValueTask AcquireAsync(CancellationToken cancellationToken = default)
        {
            using var lease = await _limiter.AcquireAsync(1, cancellationToken);
            if (!lease.IsAcquired)
                throw new BadGatewayException("Guesty rate limiter could not acquire a token.");
        }

        public ValueTask DisposeAsync()
        {
            _limiter.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
