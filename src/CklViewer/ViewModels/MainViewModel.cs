using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using CklViewer.Models;
using CklViewer.Parsing;
using CklViewer.Reports;
using CklViewer.Writing;
using Microsoft.Win32;

namespace CklViewer.ViewModels;

public class FindingRow
{
    public FindingRow(Vulnerability vulnerability, Stig stig)
    {
        Vulnerability = vulnerability;
        Stig = stig;
    }

    public Vulnerability Vulnerability { get; }
    public Stig Stig { get; }
    public string StigTitle => string.IsNullOrWhiteSpace(Stig.Title) ? Stig.StigId : Stig.Title;
}

public class MainViewModel : INotifyPropertyChanged
{
    public const string AllFilter = "All";

    private ChecklistDocument? _document;
    private FindingRow? _selectedFinding;
    private string _searchText = string.Empty;
    private string _statusFilter = AllFilter;
    private string _severityFilter = AllFilter;
    private string _stigFilter = AllFilter;
    private string _statusMessage = "Open a .ckl or .cklb checklist to begin (File → Open, or drop it on the window).";
    private string _summaryText = string.Empty;

    public MainViewModel()
    {
        Findings = new ObservableCollection<FindingRow>();
        FindingsView = CollectionViewSource.GetDefaultView(Findings);
        FindingsView.Filter = FilterFinding;

        OpenCommand = new RelayCommand(OpenChecklist);
        SaveCklCommand = new RelayCommand(SaveCkl, () => Document is not null);
        ExportCklbCommand = new RelayCommand(ExportCklb, () => Document is not null);
        ApplyScapCommand = new RelayCommand(ApplyScapResult, () => Document is not null);
        ExportReportCommand = new RelayCommand(ExportReport, () => Document is not null);
    }

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

    public IReadOnlyList<FindingStatus> StatusChoices { get; } = new[]
    {
        FindingStatus.NotReviewed, FindingStatus.Open, FindingStatus.NotAFinding, FindingStatus.NotApplicable
    };

    public IReadOnlyList<string> SeverityOverrideChoices { get; } = new[]
    {
        string.Empty, Severity.Low, Severity.Medium, Severity.High
    };

    public ICommand OpenCommand { get; }
    public ICommand SaveCklCommand { get; }
    public ICommand ExportCklbCommand { get; }
    public ICommand ApplyScapCommand { get; }
    public ICommand ExportReportCommand { get; }

    public ChecklistDocument? Document
    {
        get => _document;
        private set
        {
            _document = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Asset));
            OnPropertyChanged(nameof(HasDocument));
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public Asset? Asset => Document?.Asset;
    public bool HasDocument => Document is not null;

    public string WindowTitle => Document is null
        ? "Ckl-viewer"
        : $"Ckl-viewer — {Path.GetFileName(Document.SourcePath) ?? Document.Title}";

    public FindingRow? SelectedFinding
    {
        get => _selectedFinding;
        set
        {
            _selectedFinding = value;
            OnPropertyChanged();
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

    public void LoadChecklist(string path)
    {
        try
        {
            var document = ChecklistLoader.Load(path);
            foreach (var row in Findings)
            {
                row.Vulnerability.PropertyChanged -= OnVulnerabilityChanged;
            }

            Findings.Clear();
            StigFilters.Clear();
            StigFilters.Add(AllFilter);

            foreach (var stig in document.Stigs)
            {
                var title = string.IsNullOrWhiteSpace(stig.Title) ? stig.StigId : stig.Title;
                if (!StigFilters.Contains(title))
                {
                    StigFilters.Add(title);
                }

                foreach (var vuln in stig.Vulnerabilities)
                {
                    vuln.PropertyChanged += OnVulnerabilityChanged;
                    Findings.Add(new FindingRow(vuln, stig));
                }
            }

            Document = document;
            StigFilter = AllFilter;
            SelectedFinding = Findings.FirstOrDefault();
            UpdateSummary();
            StatusMessage = $"Loaded {Path.GetFileName(path)}: {document.Stigs.Count} STIG(s), {Findings.Count} finding(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load {Path.GetFileName(path)}: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "Unable to open checklist",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
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

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var needle = SearchText.Trim();
            return Contains(vuln.VulnId, needle) || Contains(vuln.RuleId, needle) ||
                   Contains(vuln.RuleVersion, needle) || Contains(vuln.RuleTitle, needle) ||
                   Contains(vuln.Discussion, needle) || Contains(vuln.CheckContent, needle) ||
                   Contains(vuln.FixText, needle) || Contains(vuln.CciDisplay, needle) ||
                   Contains(vuln.FindingDetails, needle) || Contains(vuln.Comments, needle);
        }

        return true;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private void UpdateSummary()
    {
        if (Document is null)
        {
            SummaryText = string.Empty;
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

        SummaryText =
            $"Total: {vulns.Count}\n" +
            $"Open: {open}  (CAT I: {catI} · CAT II: {catIi} · CAT III: {catIii})\n" +
            $"Not a Finding: {naf}\n" +
            $"Not Applicable: {na}\n" +
            $"Not Reviewed: {nr}";
    }

    private void OpenChecklist()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open STIG checklist",
            Filter = "STIG checklists (*.ckl;*.cklb)|*.ckl;*.cklb|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadChecklist(dialog.FileName);
        }
    }

    private void SaveCkl()
    {
        if (Document is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save checklist as CKL",
            Filter = "STIG Viewer 2.x checklist (*.ckl)|*.ckl",
            FileName = SuggestFileName(".ckl")
        };

        if (dialog.ShowDialog() == true)
        {
            CklWriter.WriteFile(Document, dialog.FileName);
            StatusMessage = $"Saved {Path.GetFileName(dialog.FileName)}.";
        }
    }

    private void ExportCklb()
    {
        if (Document is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export checklist as CKLB",
            Filter = "STIG Viewer 3.x checklist (*.cklb)|*.cklb",
            FileName = SuggestFileName(".cklb")
        };

        if (dialog.ShowDialog() == true)
        {
            CklbWriter.WriteFile(Document, dialog.FileName);
            StatusMessage = $"Exported {Path.GetFileName(dialog.FileName)}.";
        }
    }

    private void ApplyScapResult()
    {
        if (Document is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Apply SCAP XCCDF result",
            Filter = "XCCDF results (*.xml)|*.xml|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var outcome = XccdfResultApplier.Apply(Document, dialog.FileName);
            UpdateSummary();
            FindingsView.Refresh();
            StatusMessage =
                $"SCAP results from {outcome.BenchmarkId}: {outcome.TotalResults} result(s), " +
                $"{outcome.Matched} matched this checklist, {outcome.Updated} status(es) updated.";
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
        if (Document is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Excel report",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            FileName = SuggestFileName("_report.xlsx")
        };

        if (dialog.ShowDialog() == true)
        {
            ExcelReportGenerator.WriteReport(new[] { Document }, dialog.FileName);
            StatusMessage = $"Report written to {Path.GetFileName(dialog.FileName)}.";
        }
    }

    private string SuggestFileName(string suffix)
    {
        var baseName = Document?.SourcePath is { } source
            ? Path.GetFileNameWithoutExtension(source)
            : (string.IsNullOrWhiteSpace(Document?.Asset.HostName) ? "checklist" : Document!.Asset.HostName);
        return baseName + suffix;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
