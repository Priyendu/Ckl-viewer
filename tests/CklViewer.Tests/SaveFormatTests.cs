using CklViewer.Models;
using CklViewer.ViewModels;
using Xunit;

namespace CklViewer.Tests;

public class SaveFormatTests
{
    [Theory]
    [InlineData("C:/checklists/host.ckl", 1, ChecklistFormat.Ckl)]
    [InlineData("C:/checklists/host.cklb", 1, ChecklistFormat.Cklb)] // typed extension wins over the .ckl filter
    [InlineData("C:/checklists/host.ckl", 2, ChecklistFormat.Ckl)]   // typed extension wins over the .cklb filter
    [InlineData("C:/checklists/host", 1, ChecklistFormat.Ckl)]        // no extension: filter index decides
    [InlineData("C:/checklists/host", 2, ChecklistFormat.Cklb)]
    public void DetermineFormat_UsesExtensionThenFilter(string path, int filterIndex, ChecklistFormat expected) =>
        Assert.Equal(expected, MainViewModel.DetermineFormat(path, filterIndex));

    [Theory]
    [InlineData("host.ckl", ChecklistFormat.Cklb, "host.cklb")]
    [InlineData("host.cklb", ChecklistFormat.Ckl, "host.ckl")]
    [InlineData("host.ckl", ChecklistFormat.Ckl, "host.ckl")]
    [InlineData("host.txt", ChecklistFormat.Cklb, "host.cklb")]
    public void EnsureExtension_MatchesFormat(string path, ChecklistFormat format, string expected) =>
        Assert.Equal(expected, MainViewModel.EnsureExtension(path, format));
}
