using System.IO;
using CklViewer.Models;
using CklViewer.Parsing;
using CklViewer.ViewModels;
using CklViewer.Writing;
using Xunit;

namespace CklViewer.Tests;

public class MergeViewModelTests
{
    [Fact]
    public void MergeFrom_CarriesStatusesIntoOpenChecklistAndUpdatesDonut()
    {
        Exception? failure = null;
        var openStatus = FindingStatus.NotReviewed;
        double openSegment = -1;
        int findingsCount = 0;

        var thread = new Thread(() =>
        {
            try
            {
                // SOURCE = an assessed checklist (Open / NotAFinding / NotReviewed).
                var source = SampleData.BuildChecklist();
                var sourcePath = WriteTemp(source);

                // TARGET = the same STIG, fresh (all Not Reviewed) — the "new version".
                var target = SampleData.BuildChecklist();
                foreach (var v in target.AllVulnerabilities)
                {
                    v.Status = FindingStatus.NotReviewed;
                    v.FindingDetails = string.Empty;
                }
                var targetPath = WriteTemp(target);

                try
                {
                    var vm = new MainViewModel();
                    vm.LoadChecklists(new[] { targetPath });        // open the new version
                    var loadedSource = ChecklistLoader.Load(sourcePath);

                    vm.MergeFrom(loadedSource);                       // the real VM merge path

                    findingsCount = vm.Findings.Count;
                    openStatus = vm.Findings
                        .First(f => f.Vulnerability.RuleVersion == "WN10-00-000005")
                        .Vulnerability.Status;
                    openSegment = vm.StatusSegments.First(s => s.Label == "Open").Value;
                }
                finally
                {
                    File.Delete(sourcePath);
                    File.Delete(targetPath);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "VM merge test timed out.");

        Assert.Null(failure);
        Assert.Equal(3, findingsCount);
        Assert.Equal(FindingStatus.Open, openStatus);   // status carried into the open checklist
        Assert.Equal(1, openSegment);                    // donut recomputed: 1 Open
    }

    private static string WriteTemp(ChecklistDocument document)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ckl-merge-vm-{Guid.NewGuid():N}.ckl");
        CklWriter.WriteFile(document, path);
        return path;
    }
}
