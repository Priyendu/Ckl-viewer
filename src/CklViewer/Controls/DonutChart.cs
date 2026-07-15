using System.Windows;
using System.Windows.Media;

namespace CklViewer.Controls;

/// <summary>
/// A lightweight donut chart drawn natively with WPF geometry — no charting
/// dependency. Renders the non-zero <see cref="Segments"/> proportionally,
/// starting at the top and going clockwise.
/// </summary>
public sealed class DonutChart : FrameworkElement
{
    private static readonly Color EmptyRingColor = Color.FromRgb(0xE0, 0xE4, 0xE8);

    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments),
        typeof(IEnumerable<ChartSegment>),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<ChartSegment>? Segments
    {
        get => (IEnumerable<ChartSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var diameter = Math.Min(ActualWidth, ActualHeight);
        if (diameter <= 0)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var outer = diameter / 2 - 2;
        var inner = outer * 0.58;
        var ringRadius = (outer + inner) / 2;
        var ringThickness = outer - inner;

        var segments = (Segments ?? Enumerable.Empty<ChartSegment>()).Where(s => s.Value > 0).ToList();
        var total = segments.Sum(s => s.Value);

        if (total <= 0)
        {
            // Empty state: a faint grey ring so the panel doesn't look broken.
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(EmptyRingColor), ringThickness), center, ringRadius, ringRadius);
            return;
        }

        var startAngle = -90.0; // 12 o'clock
        foreach (var segment in segments)
        {
            var sweep = segment.Value / total * 360.0;
            var brush = new SolidColorBrush(segment.Color);
            brush.Freeze();

            // A single full-circle slice can't be expressed as an arc; draw it as a ring.
            if (sweep >= 359.999)
            {
                dc.DrawEllipse(null, new Pen(brush, ringThickness), center, ringRadius, ringRadius);
                break;
            }

            dc.DrawGeometry(brush, null, BuildSlice(center, outer, inner, startAngle, sweep));
            startAngle += sweep;
        }
    }

    private static Geometry BuildSlice(Point c, double outer, double inner, double startAngle, double sweep)
    {
        Point At(double radius, double degrees)
        {
            var rad = degrees * Math.PI / 180.0;
            return new Point(c.X + radius * Math.Cos(rad), c.Y + radius * Math.Sin(rad));
        }

        var endAngle = startAngle + sweep;
        var isLarge = sweep > 180;

        var figure = new PathFigure { StartPoint = At(outer, startAngle), IsClosed = true };
        figure.Segments.Add(new ArcSegment(At(outer, endAngle), new Size(outer, outer), 0, isLarge, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(At(inner, endAngle), true));
        figure.Segments.Add(new ArcSegment(At(inner, startAngle), new Size(inner, inner), 0, isLarge, SweepDirection.Counterclockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }
}
