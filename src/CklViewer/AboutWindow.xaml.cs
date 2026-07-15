using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace CklViewer;

public partial class AboutWindow : Window
{
    private static readonly (string Quote, string Attribution)[] Quotes =
    {
        ("Until you open the checklist, every finding is simultaneously Open and Not a Finding.",
            "— Schrödinger's STIG"),
        ("We do not inherit compliance from our predecessors; we borrow it from the next assessment.",
            "— ancient eMASS proverb"),
        ("POA&M: because 'we'll fix it later' deserved a file format.",
            "— anonymous ISSO, 3 a.m."),
        ("First they ignore your findings, then they dispute your findings, then they mark them Not Applicable.",
            "— the assessor's journey"),
        ("A CAT I closed is worth two in the mitigation plan.",
            "— risk management folklore"),
        ("The unexamined system is not worth accrediting.",
            "— Socrates, probably, had he held a security clearance"),
        ("There are two hard things in cyber security: compliance, invalidating caches, and off-by-one errors.",
            "— every engineer, eventually")
    };

    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? "" : $"Version {version.ToString(3)}";

        var (quote, attribution) = Quotes[Random.Shared.Next(Quotes.Length)];
        QuoteText.Text = $"“{quote}”";
        QuoteAttribution.Text = attribution;
    }

    private void RepoLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
