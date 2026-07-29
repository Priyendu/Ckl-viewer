using System.IO;
using ClosedXML.Excel;
using CklViewer.Reports;
using CklViewer.Settings;
using Xunit;

namespace CklViewer.Tests;

public class SettingsTests
{
    [Fact]
    public void DefaultsToStatusColoringOn()
    {
        Assert.True(new AppSettings().ColorCodeStatusInReport);
        // A missing file yields defaults, never an exception.
        Assert.True(SettingsStore.LoadFrom(Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}.json")).ColorCodeStatusInReport);
    }

    [Fact]
    public void RoundTripsThroughDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ckl-settings-{Guid.NewGuid():N}.json");
        try
        {
            SettingsStore.SaveTo(path, new AppSettings { ColorCodeStatusInReport = false });
            Assert.False(SettingsStore.LoadFrom(path).ColorCodeStatusInReport);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CorruptSettingsFileFallsBackToDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ckl-settings-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ this is not valid json ");
        try
        {
            Assert.True(SettingsStore.LoadFrom(path).ColorCodeStatusInReport);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReportColorsStatusCells_WhenEnabled()
    {
        var doc = SampleData.BuildChecklist(); // row 2 = Open, row 3 = Not a Finding, row 4 = Not Reviewed
        var path = Path.Combine(Path.GetTempPath(), $"ckl-report-{Guid.NewGuid():N}.xlsx");
        try
        {
            ExcelReportGenerator.WriteReport(new[] { doc }, path, colorCodeStatus: true);

            using var workbook = new XLWorkbook(path);
            var details = workbook.Worksheet("Vulnerability Details");
            // Colouring is done with conditional formatting (so Excel edits recolour themselves),
            // not a fill baked into each cell.
            var green = XLColor.FromArgb(0x27, 0xAE, 0x60);
            Assert.Contains(details.ConditionalFormats, f => f.Style.Fill.BackgroundColor.Equals(green));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReportDoesNotFillNonOpenStatus_WhenDisabled()
    {
        var doc = SampleData.BuildChecklist();
        var path = Path.Combine(Path.GetTempPath(), $"ckl-report-{Guid.NewGuid():N}.xlsx");
        try
        {
            ExcelReportGenerator.WriteReport(new[] { doc }, path, colorCodeStatus: false);

            using var workbook = new XLWorkbook(path);
            var details = workbook.Worksheet("Vulnerability Details");
            // With colouring off, no status rule paints a background (Open is emphasised in red text only).
            Assert.Equal(XLFillPatternValues.None, details.Row(3).Cell(8).Style.Fill.PatternType);
            var green = XLColor.FromArgb(0x27, 0xAE, 0x60);
            Assert.DoesNotContain(details.ConditionalFormats, f => f.Style.Fill.BackgroundColor.Equals(green));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
