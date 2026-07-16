using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using CklViewer.Controls;
using CklViewer.Models;
using CklViewer.Parsing;
using CklViewer.Reports;
using CklViewer.Settings;
using CklViewer.Writing;
using Microsoft.Win32;

namespace CklViewer.ViewModels;

public class FindingRow
{
    public FindingRow(Vulnerability vulnerability, Stig stig, ChecklistDocument document)
    {
        Vulnerability = vulnerability;
        Stig = stig;
        Document = document;
    }

    public Vulnerability Vulnerability { get; }
    public Stig Stig { get; }
    public ChecklistDocument Document { get; }
    public string StigTitle => string.IsNullOrWhiteSpace(Stig.Title) ? Stig.StigId : Stig.Title;

    public string AssetName => string.IsNullOrWhiteSpace(Document.Asset.HostName)
        ? Document.Title ?? "(unnamed asset)"
        : Document.Asset.HostName;
}

public class MainViewModel : INotifyPropertyChanged
{
    public const string AllFilter = "All";

    private FindingRow? _selectedFinding;
    private string _searchText = string.Empty;
    private string _statusFilter = AllFilter;
    private string _severityFilter = AllFilter;
    private string _stigFilter = AllFilter;
    private string _assetFilter = AllFilter;
    private string _statusMessage = "Open one or more .ckl / .cklb checklists (File → Open supports multi-select, or drop them on the window).";
    private string _summaryText = string.Empty;
    private IReadOnlyList<ChartSegment> _statusSegments = Array.Empty<ChartSegment>();

    public MainViewModel()
    {
        Findings = new ObservableCollection<FindingRow>();
        FindingsView = CollectionViewSource.GetDefaultView(Findings);
        FindingsView.Filter = FilterFinding;

        OpenCommand = new RelayCommand(OpenChecklists);
        NewFromStigCommand = new RelayCommand(NewFromStig);
        ClearCommand = new RelayCommand(ClearChecklists, () => Documents.Count > 0);
        SaveCommand = new RelayCommand(Save, () => CurrentDocument is not null);
        SaveAsCommand = new RelayCommand(SaveAs, () => CurrentDocument is not null);
        ApplyScapCommand = new RelayCommand(ApplyScapResult, () => Documents.Count > 0);
        ExportReportCommand = new RelayCommand(ExportReport, () => Documents.Count > 0);
    }

    /// <summary>Persisted user settings; edited via the Settings dialog and saved with <see cref="SaveSettings"/>.</summary>
    public AppSettings Settings { get; } = SettingsStore.Load();

    public void SaveSettings() => SettingsStore.Save(Settings);

    public ObservableCollection<ChecklistDocument> Documents { get; } = new();
    public ObservableCollection<FindingRow> Findings { get; }
    public ICollectionView FindingsView { get; }

    public ObservableCollection<string> StatusFilters { get; } = new(new[]
    {
        AllFilter, "Open", "Not a Finding", "Not Applicable", "Not Reviewed"
    });

    public ObservableCollection<string> SeverityFilters { get; } = new(new[]
    {
        AllFilter, "CAT I", "CAT II", "CAT III"
    });

    public ObservableCollection<string> StigFilters { get; } = new(new[] { AllFilter });
    public ObservableCollection<string> AssetFilters { get; } = new(new[] { AllFilter });

    public IReadOnlyList<FindingStatus> StatusChoices { get; } = new[]
    {
        FindingStatus.NotReviewed, FindingStatus.Open, FindingStatus.NotAFinding, FindingStatus.NotApplicable
    };

    public IReadOnlyList<string> SeverityOverrideChoices { get; } = new[]
    {
        string.Empty, Severity.Low, Severity.Medium, Severity.High
    };

    public ICommand OpenCommand { get; }
    public ICommand NewFromStigCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand ApplyScapCommand { get; }
    public ICommand ExportReportCommand { get; }

    /// <summary>The checklist that Save/Export act on: the one owning the selected finding, else the first loaded.</summary>
    public ChecklistDocument? CurrentDocument => SelectedFinding?.Document ?? Documents.FirstOrDefault();

    public Asset? Asset => CurrentDocument?.Asset;
    public bool HasDocument => Documents.Count > 0;

    public string WindowTitle => Documents.Count switch
    {
        0 => "Ckl-viewer",
        1 => $"Ckl-viewer — {Path.GetFileName(Documents[0].SourcePath) ?? Documents[0].Title}",
        _ => $"Ckl-viewer — {Documents.Count} checklists"
    };

    public FindingRow? SelectedFinding
    {
        get => _selectedFinding;
        set
        {
            _selectedFinding = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentDocument));
            OnPropertyChanged(nameof(Asset));
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); FindingsView.Refresh(); }
    }

    public string StatusFilter
    {
        get => _statusFilter;
        set { _statusFilter = value; OnPropertyChanged(); FindingsView.Refresh(); }
    }

    public string SeverityFilter
    {
        get => _severityFilter;
        set { _severityFilter = value; OnPropertyChanged(); FindingsView.Refresh(); }
    }

    public string StigFilter
    {
        get => _stigFilter;
        set { _stigFilter = value; OnPropertyChanged(); FindingsView.Refresh(); }
    }

    public string AssetFilter
    {
        get => _assetFilter;
        set { _assetFilter = value; OnPropertyChanged(); FindingsView.Refresh(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string SummaryText
    {
        get => _summaryText;
        private set { _summaryText = value; OnPropertyChanged(); }
    }

    public IReadOnlyList<ChartSegment> StatusSegments
    {
        get => _statusSegments;
        private set { _statusSegments = value; OnPropertyChanged(); }
    }

    /// <summary>Loads one or more checklist files, appending them to the current session.</summary>
    public void LoadChecklists(IEnumerable<string> paths)
    {
        var loaded = 0;
        var errors = new List<string>();

        foreach (var path in paths)
        {
            try
            {
                var document = ChecklistLoader.Load(path);
                Documents.Add(document);
                foreach (var stig in document.Stigs)
                {
                    foreach (var vuln in stig.Vulnerabilities)
                    {
                        vuln.PropertyChanged += OnVulnerabilityChanged;
                        Findings.Add(new FindingRow(vuln, stig, document));
                    }
                }

                loaded++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        RebuildFilters();
        SelectedFinding ??= Findings.FirstOrDefault();
        UpdateSummary();
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentDocument));
        OnPropertyChanged(nameof(Asset));

        StatusMessage = errors.Count == 0
            ? $"Loaded {loaded} checklist(s): {Documents.Count} total, {Findings.Count} finding(s)."
            : $"Loaded {loaded} checklist(s); {errors.Count} failed. {string.Join(" | ", errors)}";

        if (errors.Count > 0)
        {
            System.Windows.MessageBox.Show(string.Join("\n\n", errors), "Some checklists could not be opened",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    public void LoadChecklist(string path) => LoadChecklists(new[] { path });

    private void ClearChecklists()
    {
        foreach (var row in Findings)
        {
            row.Vulnerability.PropertyChanged -= OnVulnerabilityChanged;
        }

        Findings.Clear();
        Documents.Clear();
        SelectedFinding = null;
        RebuildFilters();
        UpdateSummary();
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentDocument));
        OnPropertyChanged(nameof(Asset));
        StatusMessage = "All checklists closed.";
    }

    private void RebuildFilters()
    {
        var currentStig = StigFilter;
        var currentAsset = AssetFilter;

        StigFilters.Clear();
        StigFilters.Add(AllFilter);
        AssetFilters.Clear();
        AssetFilters.Add(AllFilter);

        foreach (var row in Findings)
        {
            if (!StigFilters.Contains(row.StigTitle))
            {
                StigFilters.Add(row.StigTitle);
            }

            if (!AssetFilters.Contains(row.AssetName))
            {
                AssetFilters.Add(row.AssetName);
            }
        }

        StigFilter = StigFilters.Contains(currentStig) ? currentStig : AllFilter;
        AssetFilter = AssetFilters.Contains(currentAsset) ? currentAsset : AllFilter;
    }

    private void OnVulnerabilityChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Vulnerability.Status) or nameof(Vulnerability.SeverityOverride))
        {
            UpdateSummary();
        }
    }

    private bool FilterFinding(object item)
    {
        if (item is not FindingRow row)
        {
            return false;
        }

        var vuln = row.Vulnerability;
        if (StatusFilter != AllFilter && vuln.Status.ToDisplayString() != StatusFilter)
        {
            return false;
        }

        if (SeverityFilter != AllFilter && vuln.Category != SeverityFilter)
        {
            return false;
        }

        if (StigFilter != AllFilter && row.StigTitle != StigFilter)
        {
            return false;
        }

        if (AssetFilter != AllFilter && row.AssetName != AssetFilter)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var needle = SearchText.Trim();
            return Contains(vuln.VulnId, needle) || Contains(vuln.RuleId, needle) ||
                   Contains(vuln.RuleVersion, needle) || Contains(vuln.RuleTitle, needle) ||
                   Contains(vuln.Discussion, needle) || Contains(vuln.CheckContent, needle) ||
                   Contains(vuln.FixText, needle) || Contains(vuln.CciDisplay, needle) ||
                   Contains(vuln.FindingDetails, needle) || Contains(vuln.Comments, needle) ||
                   Contains(row.AssetName, needle);
        }

        return true;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private void UpdateSummary()
    {
        if (Documents.Count == 0)
        {
            SummaryText = string.Empty;
            StatusSegments = Array.Empty<ChartSegment>();
            return;
        }

        var vulns = Findings.Select(f => f.Vulnerability).ToList();
        var open = vulns.Count(v => v.Status == FindingStatus.Open);
        var catI = vulns.Count(v => v.Status == FindingStatus.Open && v.EffectiveSeverity == Severity.High);
        var catIi = vulns.Count(v => v.Status == FindingStatus.Open && v.EffectiveSeverity == Severity.Medium);
        var catIii = vulns.Count(v => v.Status == FindingStatus.Open && v.EffectiveSeverity == Severity.Low);
        var naf = vulns.Count(v => v.Status == FindingStatus.NotAFinding);
        var na = vulns.Count(v => v.Status == FindingStatus.NotApplicable);
        var nr = vulns.Count(v => v.Status == FindingStatus.NotReviewed);

        StatusSegments = ChartSegment.StatusBreakdown(open, naf, na, nr);

        SummaryText =
            $"Checklists: {Documents.Count}\n" +
            $"Total: {vulns.Count}\n" +
            $"Open: {open}  (CAT I: {catI} · CAT II: {catIi} · CAT III: {catIii})\n" +
            $"Not a Finding: {naf}\n" +
            $"Not Applicable: {na}\n" +
            $"Not Reviewed: {nr}";
    }

    private void OpenChecklists()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open STIG checklists (multi-select supported)",
            Filter = "STIG checklists (*.ckl;*.cklb)|*.ckl;*.cklb|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            LoadChecklists(dialog.FileNames);
        }
    }

    private void NewFromStig()
    {
        var dialog = new OpenFileDialog
        {
            Title = "New checklist from a STIG benchmark",
            Filter = "STIG benchmark (*.xml;*.zip)|*.xml;*.zip|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            var before = Documents.Count;
            LoadChecklists(dialog.FileNames);
            var created = Documents.Count - before;
            if (created > 0)
            {
                var findings = Findings.Count(f => f.Vulnerability.Status == FindingStatus.NotReviewed);
                StatusMessage = $"Created {created} checklist(s) from STIG benchmark(s). " +
                                $"All findings start as Not Reviewed ({findings} awaiting review). Save as .ckl or .cklb when ready.";
            }
        }
    }

    /// <summary>Writes back to the file the checklist was opened from, in its original format.</summary>
    private void Save()
    {
        var document = CurrentDocument;
        if (document is null)
        {
            return;
        }

        // No known file yet (e.g. imported from a STIG benchmark) — fall through to Save As.
        if (string.IsNullOrWhiteSpace(document.SourcePath))
        {
            SaveAs();
            return;
        }

        WriteDocument(document, document.SourcePath!, document.SourceFormat);
        StatusMessage = $"Saved {Path.GetFileName(document.SourcePath)}.";
    }

    /// <summary>Prompts for a location and format (.ckl or .cklb) and saves a copy, then edits it going forward.</summary>
    private void SaveAs()
    {
        var document = CurrentDocument;
        if (document is null)
        {
            return;
        }

        var startAsCklb = document.SourceFormat == ChecklistFormat.Cklb;
        var dialog = new SaveFileDialog
        {
            Title = $"Save checklist as… — {AssetLabel(document)}",
            Filter = "STIG Viewer 2.x checklist (*.ckl)|*.ckl|STIG Viewer 3.x checklist (*.cklb)|*.cklb",
            FilterIndex = startAsCklb ? 2 : 1,
            FileName = SuggestFileName(document, startAsCklb ? ".cklb" : ".ckl")
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var format = DetermineFormat(dialog.FileName, dialog.FilterIndex);
        var path = EnsureExtension(dialog.FileName, format);
        WriteDocument(document, path, format);

        // From here on, plain Save writes back to this new file and format.
        document.SourcePath = path;
        document.SourceFormat = format;
        OnPropertyChanged(nameof(WindowTitle));
        StatusMessage = $"Saved {Path.GetFileName(path)} ({(format == ChecklistFormat.Cklb ? "CKLB" : "CKL")}).";
    }

    private static void WriteDocument(ChecklistDocument document, string path, ChecklistFormat format)
    {
        if (format == ChecklistFormat.Cklb)
        {
            CklbWriter.WriteFile(document, path);
        }
        else
        {
            CklWriter.WriteFile(document, path);
        }
    }

    /// <summary>A typed extension wins over the chosen filter; otherwise the filter picks the format.</summary>
    internal static ChecklistFormat DetermineFormat(string path, int filterIndex)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".cklb" => ChecklistFormat.Cklb,
            ".ckl" => ChecklistFormat.Ckl,
            _ => filterIndex == 2 ? ChecklistFormat.Cklb : ChecklistFormat.Ckl
        };
    }

    internal static string EnsureExtension(string path, ChecklistFormat format)
    {
        var wanted = format == ChecklistFormat.Cklb ? ".cklb" : ".ckl";
        return Path.GetExtension(path).Equals(wanted, StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.ChangeExtension(path, wanted);
    }

    private void ApplyScapResult()
    {
        if (Documents.Count == 0)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Apply SCAP XCCDF results (multi-select supported)",
            Filter = "XCCDF results (*.xml)|*.xml|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            int matched = 0, updated = 0, total = 0;
            foreach (var file in dialog.FileNames)
            {
                foreach (var document in Documents)
                {
                    var outcome = XccdfResultApplier.Apply(document, file);
                    matched += outcome.Matched;
                    updated += outcome.Updated;
                    total = Math.Max(total, outcome.TotalResults);
                }
            }

            UpdateSummary();
            FindingsView.Refresh();
            StatusMessage =
                $"SCAP results applied across {Documents.Count} checklist(s): {matched} match(es), {updated} status(es) updated.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to apply SCAP result: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "Unable to apply SCAP result",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void ExportReport()
    {
        if (Documents.Count == 0)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = $"Export Excel report ({Documents.Count} checklist(s))",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            FileName = Documents.Count == 1
                ? SuggestFileName(Documents[0], "_report.xlsx")
                : $"stig_report_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (dialog.ShowDialog() == true)
        {
            ExcelReportGenerator.WriteReport(Documents.ToList(), dialog.FileName, Settings.ColorCodeStatusInReport);
            StatusMessage = $"Report for {Documents.Count} checklist(s) written to {Path.GetFileName(dialog.FileName)}.";
        }
    }

    private static string AssetLabel(ChecklistDocument document) =>
        string.IsNullOrWhiteSpace(document.Asset.HostName)
            ? document.Title ?? "(unnamed asset)"
            : document.Asset.HostName;

    private static string SuggestFileName(ChecklistDocument document, string suffix)
    {
        var baseName = document.SourcePath is { } source
            ? Path.GetFileNameWithoutExtension(source)
            : (string.IsNullOrWhiteSpace(document.Asset.HostName) ? "checklist" : document.Asset.HostName);
        return SanitizeFileName(baseName) + suffix;
    }

    /// <summary>Host names come from untrusted checklist files; strip characters Windows rejects in file names.</summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "checklist" : cleaned;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
