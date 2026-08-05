using System.IO;
using CklViewer.Models;
using CklViewer.ViewModels;
using CklViewer.Writing;
using Xunit;

namespace CklViewer.Tests;

public class FilterCountTests
{
    /// <summary>Runs the view-model work on an STA thread, as WPF collection views require.</summary>
    private static void OnStaThread(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Filter test timed out.");
        Assert.Null(failure);
    }

    private static string WriteSample()
    {
        // 3 findings: 1 Open, 1 Not a Finding, 1 Not Reviewed.
        var path = Path.Combine(Path.GetTempPath(), $"ckl-filter-{Guid.NewGuid():N}.ckl");
        CklWriter.WriteFile(SampleData.BuildChecklist(), path);
        return path;
    }

    [Fact]
    public void CountsAndBreakdownFollowTheFilter()
    {
        OnStaThread(() =>
        {
            var path = WriteSample();
            try
            {
                var vm = new MainViewModel();
                vm.LoadChecklists(new[] { path });

                // Unfiltered: plain total, no "(filtered)" wording.
                Assert.False(vm.HasActiveFilter);
                Assert.Equal(3, vm.VisibleCount);
                Assert.Equal("3 findings", vm.VisibleCountText);
                Assert.Equal("Status breakdown", vm.SummaryHeader);
                Assert.Equal(1, vm.StatusSegments.First(s => s.Label == "Open").Value);

                // Filter to Open only.
                vm.StatusFilter = "Open";

                Assert.True(vm.HasActiveFilter);
                Assert.Equal(1, vm.VisibleCount);
                Assert.Equal("Showing 1 of 3 findings", vm.VisibleCountText);
                Assert.Equal("Status breakdown (filtered)", vm.SummaryHeader);

                // The chart now reflects only what's visible.
                Assert.Equal(1, vm.StatusSegments.First(s => s.Label == "Open").Value);
                Assert.Equal(0, vm.StatusSegments.First(s => s.Label == "Not a Finding").Value);
                Assert.Contains("Shown: 1 of 3", vm.SummaryText);

                // Clearing the filter restores the full picture.
                vm.StatusFilter = MainViewModel.AllFilter;
                Assert.False(vm.HasActiveFilter);
                Assert.Equal(3, vm.VisibleCount);
                Assert.Equal(1, vm.StatusSegments.First(s => s.Label == "Not a Finding").Value);
            }
            finally
            {
                File.Delete(path);
            }
        });
    }

    [Fact]
    public void SearchTextAlsoCountsAsAFilter()
    {
        OnStaThread(() =>
        {
            var path = WriteSample();
            try
            {
                var vm = new MainViewModel();
                vm.LoadChecklists(new[] { path });

                vm.SearchText = "WN10-00-000005"; // matches exactly one rule

                Assert.True(vm.HasActiveFilter);
                Assert.Equal(1, vm.VisibleCount);
                Assert.Equal("Showing 1 of 3 findings", vm.VisibleCountText);
                Assert.Equal("Status breakdown (filtered)", vm.SummaryHeader);

                vm.SearchText = string.Empty;
                Assert.False(vm.HasActiveFilter);
                Assert.Equal("3 findings", vm.VisibleCountText);
            }
            finally
            {
                File.Delete(path);
            }
        });
    }

    [Fact]
    public void EmptySessionShowsNoCount()
    {
        OnStaThread(() =>
        {
            var vm = new MainViewModel();
            Assert.Equal(string.Empty, vm.VisibleCountText);
            Assert.Equal(0, vm.VisibleCount);
        });
    }
}
