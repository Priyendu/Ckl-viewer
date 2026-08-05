using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

    // Slice layout captured at render time, used for hover hit-testing.
    private readonly List<(ChartSegment Segment, double StartAngle, double Sweep)> _slices = new();
    private ChartSegment? _hovered;
    private Point _center;
    private double _innerRadius;
    private double _outerRadius;
    private double _total;

    public DonutChart()
    {
        // Show the slice tooltip promptly and follow the pointer, like a chart should.
        ToolTipService.SetInitialShowDelay(this, 150);
        ToolTipService.SetBetweenShowDelay(this, 0);
        ToolTipService.SetShowDuration(this, 30000);
        ToolTipService.SetPlacement(this, PlacementMode.Mouse);
    }

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

        // Remember the layout so hover can work out which slice the pointer is over.
        _center = center;
        _innerRadius = inner;
        _outerRadius = outer;
        _slices.Clear();

        var segments = (Segments ?? Enumerable.Empty<ChartSegment>()).Where(s => s.Value > 0).ToList();
        var total = segments.Sum(s => s.Value);
        _total = total;

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
                _slices.Add((segment, startAngle, 360.0));
                break;
            }

            dc.DrawGeometry(brush, null, BuildSlice(center, outer, inner, startAngle, sweep));
            _slices.Add((segment, startAngle, sweep));
            startAngle += sweep;
        }
    }

    /// <summary>Returns the slice under a point, or null when the point misses the ring.</summary>
    internal ChartSegment? HitTestSegment(Point point)
    {
        if (_slices.Count == 0 || _total <= 0)
        {
            return null;
        }

        var dx = point.X - _center.X;
        var dy = point.Y - _center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < _innerRadius || distance > _outerRadius)
        {
            return null; // in the hole, or outside the ring
        }

        // Slices are laid out from -90° clockwise, so normalise into [-90, 270).
        var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        if (angle < -90.0)
        {
            angle += 360.0;
        }

        foreach (var (segment, start, sweep) in _slices)
        {
            if (angle >= start && angle < start + sweep)
            {
                return segment;
            }
        }

        return null;
    }

    /// <summary>"Open: 4 (33.3%)" — the text shown when hovering a slice.</summary>
    internal string DescribeSlice(ChartSegment segment)
    {
        var percent = _total <= 0 ? 0 : segment.Value / _total * 100.0;
        return $"{segment.Label}: {segment.Value:N0} ({percent:0.#}%)";
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var segment = HitTestSegment(e.GetPosition(this));
        if (ReferenceEquals(segment, _hovered))
        {
            return; // still on the same slice; leave the tooltip alone
        }

        // Swapping the ToolTip closes the previous one and lets the next slice's text show.
        _hovered = segment;
        ToolTip = segment is null ? null : DescribeSlice(segment);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = null;
        ToolTip = null;
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
