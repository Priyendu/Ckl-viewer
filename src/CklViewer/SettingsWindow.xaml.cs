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
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // Write the edited values back only on OK, so Cancel leaves settings untouched.
        _settings.ColorCodeStatusInReport = ColorStatusCheck.IsChecked == true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
