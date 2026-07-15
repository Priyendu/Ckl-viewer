using System.Windows;
using CklViewer.ViewModels;

namespace CklViewer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        var files = Environment.GetCommandLineArgs().Skip(1).Where(System.IO.File.Exists).ToArray();
        if (files.Length > 0)
        {
            _viewModel.LoadChecklists(files);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            _viewModel.LoadChecklists(files);
        }
    }
}
