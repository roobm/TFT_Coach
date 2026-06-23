namespace TFTCoach.Infrastructure.RiotApi;

/// <summary>
/// Enforces Riot's developer rate limits across all calls sharing one API key:
/// 20 requests per 1 second and 100 requests per 2 minutes. Acquiring a slot
/// blocks (asynchronously) until both windows have capacity.
/// </summary>
public sealed class RiotRateLimiter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Window _perSecond = new(20, TimeSpan.FromSeconds(1));
    private readonly Window _perTwoMinutes = new(100, TimeSpan.FromMinutes(2));

    /// <summary>
    /// Waits until both rate-limit windows can accept another request, then
    /// records the request against both windows.
    /// </summary>
    public async Task AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                var now = DateTimeOffset.UtcNow;
                _perSecond.Trim(now);
                _perTwoMinutes.Trim(now);

                if (_perSecond.HasCapacity && _perTwoMinutes.HasCapacity)
                {
                    _perSecond.Record(now);
                    _perTwoMinutes.Record(now);
                    return;
                }

                var wait = Max(_perSecond.TimeUntilSlot(now), _perTwoMinutes.TimeUntilSlot(now));
                if (wait < TimeSpan.FromMilliseconds(10))
                {
                    wait = TimeSpan.FromMilliseconds(10);
                }

                await Task.Delay(wait, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private sealed class Window(int limit, TimeSpan duration)
    {
        private readonly Queue<DateTimeOffset> _hits = new();

        public bool HasCapacity => _hits.Count < limit;

        public void Trim(DateTimeOffset now)
        {
            while (_hits.Count > 0 && now - _hits.Peek() >= duration)
            {
                _hits.Dequeue();
            }
        }

        public void Record(DateTimeOffset now) => _hits.Enqueue(now);

        public TimeSpan TimeUntilSlot(DateTimeOffset now) =>
            _hits.Count == 0 ? TimeSpan.Zero : duration - (now - _hits.Peek());
    }
}
