using System.Windows;
using Xunit;

namespace CklViewer.Tests;

public class AboutWindowTests
{
    [Fact]
    public void AboutWindowConstructsWithQuoteVersionAndAuthor()
    {
        Exception? failure = null;
        string? quote = null, version = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current is null)
                {
                    _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                }

                var window = new AboutWindow();
                quote = window.QuoteText.Text;
                version = window.VersionText.Text;
                window.Close();
                Application.Current?.Dispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "About window construction timed out.");

        Assert.Null(failure);
        Assert.False(string.IsNullOrWhiteSpace(quote));
        Assert.StartsWith("Version 1.", version);
    }
}
