using XUnlock.Services;

namespace XUnlock.Tests;

public class TimingEngineTests
{
    [Fact]
    public void KeepsBoundedSampleWindow()
    {
        var e = new TimingEngine();
        for (var i = 1; i <= 100; i++) e.AddSample(TimeSpan.FromMilliseconds(i), null);
        var s = e.Snapshot("TEST");
        Assert.True(s.RttMs >= 40 && s.RttMs <= 70);
        Assert.True(s.P90Ms >= 80);
    }

    [Fact]
    public void CalculatesServerOffsetUsingMidpoint()
    {
        var e = new TimingEngine();
        var now = DateTimeOffset.UtcNow;
        e.AddSample(TimeSpan.FromMilliseconds(100), now.AddMilliseconds(300));
        var s = e.Snapshot("TEST");
        Assert.True(s.OffsetMs > 200 && s.OffsetMs < 400);
    }
}
