using System.IO;
using System.Text.Json;

namespace CklViewer.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON. Never throws — a bad or
/// missing file falls back to defaults, so settings can never block the app from starting.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Ckl-viewer",
        "settings.json");

    public static AppSettings Load() => LoadFrom(DefaultPath);

    public static AppSettings LoadFrom(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt or unreadable settings file — fall back to defaults.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings) => SaveTo(DefaultPath, settings);

    public static void SaveTo(string path, AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // Best-effort persistence; a failure here shouldn't crash the app.
        }
    }
}
