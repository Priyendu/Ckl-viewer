using System.Globalization;
using System.Windows.Data;
using CklViewer.Models;

namespace CklViewer.Converters;

public class StatusDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is FindingStatus status ? status.ToDisplayString() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        FindingStatusExtensions.Parse(value?.ToString());
}

public class SeverityDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var severity = value as string;
        return string.IsNullOrWhiteSpace(severity) ? "(none)" : $"{Models.Severity.ToCategory(severity)} ({severity})";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
