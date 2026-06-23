using System.Diagnostics;
using TFTCoach.Infrastructure.RiotApi;

namespace TFTCoach.Infrastructure.Tests.RiotApi;

public class RiotRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_ShouldThrottle_WhenPerSecondLimitExceeded()
    {
        // Arrange
        var limiter = new RiotRateLimiter();

        // Act: the first 20 acquisitions fit in the 1-second window.
        for (var i = 0; i < 20; i++)
        {
            await limiter.AcquireAsync();
        }

        var stopwatch = Stopwatch.StartNew();
        await limiter.AcquireAsync(); // 21st must wait for the window to roll over.
        stopwatch.Stop();

        // Assert
        Assert.True(
            stopwatch.ElapsedMilliseconds >= 800,
            $"Expected the 21st request to wait ~1s but waited {stopwatch.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task AcquireAsync_ShouldNotThrottle_WhenWithinLimit()
    {
        // Arrange
        var limiter = new RiotRateLimiter();

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
        {
            await limiter.AcquireAsync();
        }
        stopwatch.Stop();

        // Assert
        Assert.True(
            stopwatch.ElapsedMilliseconds < 500,
            $"Expected 20 requests to pass without throttling but took {stopwatch.ElapsedMilliseconds}ms.");
    }
}
