using System.Windows.Media;

namespace CklViewer.Controls;

/// <summary>One slice of a <see cref="DonutChart"/>: a label, its value, and its color.</summary>
public sealed class ChartSegment
{
    public required string Label { get; init; }
    public double Value { get; init; }
    public Color Color { get; init; }

    // Deeper shades of the grid's status-chip colors, chosen for contrast in a filled slice.
    private static readonly Color OpenColor = Color.FromRgb(0xE7, 0x4C, 0x3C);
    private static readonly Color NotAFindingColor = Color.FromRgb(0x27, 0xAE, 0x60);
    private static readonly Color NotApplicableColor = Color.FromRgb(0x9A, 0xA7, 0xAD);
    private static readonly Color NotReviewedColor = Color.FromRgb(0xF0, 0xB4, 0x00);

    /// <summary>Builds the four status slices in the order the summary lists them.</summary>
    public static IReadOnlyList<ChartSegment> StatusBreakdown(int open, int notAFinding, int notApplicable, int notReviewed) =>
        new[]
        {
            new ChartSegment { Label = "Open", Value = open, Color = OpenColor },
            new ChartSegment { Label = "Not a Finding", Value = notAFinding, Color = NotAFindingColor },
            new ChartSegment { Label = "Not Applicable", Value = notApplicable, Color = NotApplicableColor },
            new ChartSegment { Label = "Not Reviewed", Value = notReviewed, Color = NotReviewedColor }
        };
}
