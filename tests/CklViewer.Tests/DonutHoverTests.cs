using System.Windows;
using CklViewer.Controls;
using Xunit;

namespace CklViewer.Tests;

public class DonutHoverTests
{
    /// <summary>Lays out a 200x200 donut with the given segments and returns it, ready to hit-test.</summary>
    private static DonutChart Render(params ChartSegment[] segments)
    {
        DonutChart? chart = null;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                chart = new DonutChart { Segments = segments };
                chart.Measure(new Size(200, 200));
                chart.Arrange(new Rect(0, 0, 200, 200));
                // Force a render pass so the slice layout is captured.
                chart.UpdateLayout();
                var visual = new System.Windows.Media.DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    typeof(DonutChart)
                        .GetMethod("OnRender", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                        .Invoke(chart, new object[] { dc });
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Donut render timed out.");
        Assert.Null(failure);
        return chart!;
    }

    private static ChartSegment Seg(string label, double value) =>
        new() { Label = label, Value = value };

    [Fact]
    public void HoverOverASliceIdentifiesIt()
    {
        // Two equal halves: first sweeps from 12 o'clock clockwise to 6 o'clock (the right half).
        var chart = Render(Seg("Open", 5), Seg("Not a Finding", 5));

        // Centre is (100,100); ring sits between r=~55 and r=~98. Sample at r=75.
        var right = new Point(100 + 75, 100);  // 3 o'clock -> first segment
        var left = new Point(100 - 75, 100);   // 9 o'clock -> second segment

        Assert.Equal("Open", chart.HitTestSegment(right)?.Label);
        Assert.Equal("Not a Finding", chart.HitTestSegment(left)?.Label);
    }

    [Fact]
    public void HoverInTheHoleOrOutsideMissesEverything()
    {
        var chart = Render(Seg("Open", 5), Seg("Not a Finding", 5));

        Assert.Null(chart.HitTestSegment(new Point(100, 100)));   // centre hole
        Assert.Null(chart.HitTestSegment(new Point(199, 199)));   // outside the ring
    }

    [Fact]
    public void SingleStatusFillsTheWholeRing()
    {
        var chart = Render(Seg("Not Reviewed", 12));

        // Every point on the ring belongs to the only segment.
        Assert.Equal("Not Reviewed", chart.HitTestSegment(new Point(100 + 75, 100))?.Label);
        Assert.Equal("Not Reviewed", chart.HitTestSegment(new Point(100, 100 - 75))?.Label);
        Assert.Equal("Not Reviewed", chart.HitTestSegment(new Point(100 - 75, 100))?.Label);
    }

    [Fact]
    public void EmptyChartHasNothingToHover()
    {
        var chart = Render();
        Assert.Null(chart.HitTestSegment(new Point(100 + 75, 100)));
    }

    [Fact]
    public void HoveringPopulatesTheAttachedTooltip()
    {
        DonutChart? chart = null;
        string? overSlice = null;
        object? tooltipObject = null;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                chart = new DonutChart { Segments = new[] { Seg("Open", 4), Seg("Not a Finding", 8) } };
                chart.Measure(new Size(200, 200));
                chart.Arrange(new Rect(0, 0, 200, 200));
                var visual = new System.Windows.Media.DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    typeof(DonutChart)
                        .GetMethod("OnRender", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                        .Invoke(chart, new object[] { dc });
                }

                chart.UpdateHover(new Point(100 + 75, 100)); // 3 o'clock -> the Open slice
                overSlice = chart.TooltipContent;
                tooltipObject = chart.ToolTip;

                // Moving into the hole detaches the tooltip again.
                chart.UpdateHover(new Point(100, 100));
                Assert.Null(chart.TooltipContent);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Hover test timed out.");
        Assert.Null(failure);

        Assert.NotNull(tooltipObject);
        Assert.Equal("Open: 4 (33.3%)", overSlice);
    }

    [Fact]
    public void TooltipTextCarriesCountAndPercentage()
    {
        // 4 Open out of 12 total = 33.3%.
        var chart = Render(Seg("Open", 4), Seg("Not a Finding", 5), Seg("Not Reviewed", 3));

        var open = chart.HitTestSegment(new Point(100 + 75, 100));
        Assert.NotNull(open);
        Assert.Equal("Open: 4 (33.3%)", chart.DescribeSlice(open!));
    }

    [Fact]
    public void WholeRingReadsAsOneHundredPercent()
    {
        var chart = Render(Seg("Not Reviewed", 12));
        var only = chart.HitTestSegment(new Point(100 + 75, 100));

        Assert.NotNull(only);
        Assert.Equal("Not Reviewed: 12 (100%)", chart.DescribeSlice(only!));
    }
}
