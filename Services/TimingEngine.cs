using XUnlock.Models;

namespace XUnlock.Services;

public sealed class TimingEngine
{
    private readonly Queue<double> _samples = new();
    private readonly object _gate = new();
    private double _offsetMs;

    public void AddSample(TimeSpan rtt, DateTimeOffset? serverDate)
    {
        lock (_gate)
        {
            _samples.Enqueue(Math.Max(0, rtt.TotalMilliseconds));
            while (_samples.Count > 60) _samples.Dequeue();
            if (serverDate.HasValue)
            {
                var midpoint = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(rtt.TotalMilliseconds / 2);
                _offsetMs = (serverDate.Value - midpoint).TotalMilliseconds;
            }
        }
    }

    public TimingSnapshot Snapshot(string phase)
    {
        lock (_gate)
        {
            var arr = _samples.OrderBy(x => x).ToArray();
            var median = arr.Length == 0 ? 0 : arr[arr.Length / 2];
            var p90 = arr.Length == 0 ? 0 : arr[(int)Math.Floor((arr.Length - 1) * 0.90)];
            var serverNow = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(_offsetMs);
            return new(median, p90, _offsetMs, serverNow, phase);
        }
    }
}
