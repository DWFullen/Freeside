using Freeside.Infrastructure.Messaging;

namespace Freeside.Infrastructure.Tests.Messaging;

public sealed class BackoffTests
{
    private static readonly TimeSpan _base = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _cap = TimeSpan.FromHours(1);

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(10, 2560)]
    [InlineData(11, 3600)]
    [InlineData(100, 3600)]
    [InlineData(int.MaxValue, 3600)]
    public void Without_jitter_the_delay_doubles_up_to_the_cap(int attempt, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), Backoff.After(attempt, _base, _cap, new EdgeRandom(highest: false)));

    [Theory]
    [InlineData(1, 10)]
    [InlineData(10, 2565)]
    [InlineData(11, 3600)]
    public void Jitter_adds_at_most_one_base_and_never_passes_the_cap(int attempt, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), Backoff.After(attempt, _base, _cap, new EdgeRandom(highest: true)));

    [Fact]
    public void Random_jitter_stays_within_its_bounds()
    {
        var random = new Random(20260930);
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            var floor = TimeSpan.FromTicks(Math.Min(_base.Ticks << (attempt - 1), _cap.Ticks));
            for (var i = 0; i < 200; i++)
            {
                var delay = Backoff.After(attempt, _base, _cap, random);
                Assert.InRange(delay, floor, floor + _base > _cap ? _cap : floor + _base);
            }
        }
    }

    [Theory]
    [InlineData(0, 3600)]
    [InlineData(-5, 3600)]
    [InlineData(10, 5)]
    public void The_base_must_be_positive_and_no_more_than_the_cap(int baseSeconds, int capSeconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Backoff.After(1, TimeSpan.FromSeconds(baseSeconds), TimeSpan.FromSeconds(capSeconds), Random.Shared));

    /// <summary>Always returns the lowest or the highest value it may.</summary>
    private sealed class EdgeRandom(bool highest) : Random
    {
        public override long NextInt64(long minValue, long maxValue) => highest ? maxValue - 1 : minValue;
    }
}
