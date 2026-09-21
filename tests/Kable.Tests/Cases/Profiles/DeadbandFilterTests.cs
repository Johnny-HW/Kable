using Kable.Engine.Profiles;
using Xunit;

namespace Kable.Tests.Cases.Profiles;

public sealed class DeadbandFilterTests
{
    [Fact]
    public void DeadbandFilter_Double_ShouldFilterMinorJitter()
    {
        var filter = new DeadbandFilter<double>(0.5);

        // First value always passes
        Assert.True(filter.ShouldPublish(100.0, out var val1));
        Assert.Equal(100.0, val1);

        // Minor change within deadband (< 0.5) should be suppressed
        Assert.False(filter.ShouldPublish(100.3, out _));
        Assert.False(filter.ShouldPublish(100.49, out _));
        Assert.Equal(100.0, filter.LastPublishedValue);

        // Exceeding deadband should pass and update baseline
        Assert.True(filter.ShouldPublish(100.6, out var val2));
        Assert.Equal(100.6, val2);
        Assert.Equal(100.6, filter.LastPublishedValue);
    }

    [Fact]
    public void DeadbandFilter_Int_ShouldFilterSmallChanges()
    {
        var filter = new DeadbandFilter<int>(5);

        Assert.True(filter.ShouldPublish(10, out _));
        Assert.False(filter.ShouldPublish(14, out _));
        Assert.False(filter.ShouldPublish(8, out _));

        // +6 diff
        Assert.True(filter.ShouldPublish(16, out var val));
        Assert.Equal(16, val);
    }

    [Fact]
    public void DeadbandFilter_Reset_ClearsBaseline()
    {
        var filter = new DeadbandFilter<double>(1.0);

        filter.ShouldPublish(50.0, out _);
        Assert.True(filter.HasValue);

        filter.Reset();
        Assert.False(filter.HasValue);

        // After reset, 50.2 will pass because it's considered first value
        Assert.True(filter.ShouldPublish(50.2, out var val));
        Assert.Equal(50.2, val);
    }
}
