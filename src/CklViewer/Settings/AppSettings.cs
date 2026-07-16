namespace CklViewer.Settings;

/// <summary>User-configurable options, persisted to %AppData%\Ckl-viewer\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>
    /// When true, the Status column in the Excel report's Vulnerability Details sheet
    /// is filled with the finding's status color (matching the in-app status donut).
    /// </summary>
    public bool ColorCodeStatusInReport { get; set; } = true;
}
