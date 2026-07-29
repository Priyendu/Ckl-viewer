using System.Windows;
using CklViewer.Settings;

namespace CklViewer;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ColorStatusCheck.IsChecked = settings.ColorCodeStatusInReport;
        InternalNotesCheck.IsChecked = settings.IncludeInternalNotesColumn;
        ResetChangedCheck.IsChecked = settings.ResetChangedRulesOnMerge;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // Write the edited values back only on OK, so Cancel leaves settings untouched.
        _settings.ColorCodeStatusInReport = ColorStatusCheck.IsChecked == true;
        _settings.IncludeInternalNotesColumn = InternalNotesCheck.IsChecked == true;
        _settings.ResetChangedRulesOnMerge = ResetChangedCheck.IsChecked == true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
