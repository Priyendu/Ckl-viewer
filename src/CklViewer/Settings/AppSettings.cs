namespace CklViewer.Settings;

/// <summary>User-configurable options, persisted to %AppData%\Ckl-viewer\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>
    /// When true, the Status column in the Excel report's Vulnerability Details sheet
    /// is filled with the finding's status color (matching the in-app status donut).
    /// </summary>
    public bool ColorCodeStatusInReport { get; set; } = true;

    /// <summary>
    /// When true, the Excel report's Vulnerability Details sheet gets a trailing
    /// "Internal Notes" column for team-only notes. These are never written to
    /// .ckl / .cklb, but they survive an Excel export/import round trip.
    /// </summary>
    public bool IncludeInternalNotesColumn { get; set; } = true;

    /// <summary>
    /// When true, merging a prior assessment resets any rule whose check/fix text changed
    /// between versions to Not Reviewed. When false (default), the prior status is carried
    /// forward and the finding is flagged for re-verification instead.
    /// </summary>
    public bool ResetChangedRulesOnMerge { get; set; }
}
