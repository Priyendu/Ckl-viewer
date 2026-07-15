using CklViewer.Controls;
using Xunit;

namespace CklViewer.Tests;

public class StatusChartTests
{
    [Fact]
    public void StatusBreakdown_MapsCountsToLabelledSlices()
    {
        var segments = ChartSegment.StatusBreakdown(open: 4, notAFinding: 5, notApplicable: 1, notReviewed: 2);

        Assert.Equal(4, segments.Count);
        Assert.Collection(segments,
            s => { Assert.Equal("Open", s.Label); Assert.Equal(4, s.Value); },
            s => { Assert.Equal("Not a Finding", s.Label); Assert.Equal(5, s.Value); },
            s => { Assert.Equal("Not Applicable", s.Label); Assert.Equal(1, s.Value); },
            s => { Assert.Equal("Not Reviewed", s.Label); Assert.Equal(2, s.Value); });
    }

    [Fact]
    public void StatusBreakdown_UsesDistinctColorsPerStatus()
    {
        var segments = ChartSegment.StatusBreakdown(1, 1, 1, 1);
        var colors = segments.Select(s => s.Color).ToHashSet();

        Assert.Equal(4, colors.Count);
    }

    [Fact]
    public void StatusBreakdown_KeepsZeroSlicesForTheLegend()
    {
        // The legend lists every status even at zero; the donut itself skips zero slices.
        var segments = ChartSegment.StatusBreakdown(0, 0, 0, 0);

        Assert.Equal(4, segments.Count);
        Assert.All(segments, s => Assert.Equal(0, s.Value));
    }
}
