using System.Net;
using Microsoft.Extensions.Logging;

namespace TFTCoach.Infrastructure.RiotApi;

/// <summary>
/// Throttles outgoing Riot API requests through <see cref="RiotRateLimiter"/>
/// and, if the API still returns 429, honours the <c>Retry-After</c> header and
/// retries a limited number of times.
/// </summary>
public sealed class RiotRateLimitHandler(
    RiotRateLimiter limiter,
    ILogger<RiotRateLimitHandler> logger) : DelegatingHandler
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var current = request;

        for (var attempt = 0; ; attempt++)
        {
            await limiter.AcquireAsync(cancellationToken);

            var response = await base.SendAsync(current, cancellationToken);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= MaxRetries)
            {
                return response;
            }

            var retryAfter = response.Headers.RetryAfter?.Delta ?? DefaultRetryAfter;
            logger.LogWarning(
                "Riot API returned 429; retrying after {Delay}s (attempt {Attempt} of {Max}).",
                retryAfter.TotalSeconds, attempt + 1, MaxRetries);

            response.Dispose();
            await Task.Delay(retryAfter, cancellationToken);

            // HttpRequestMessage cannot be reused once sent; clone for the retry.
            current = Clone(current);
        }
    }

    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
