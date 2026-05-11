// Views/LogViewerWindow.axaml.cs

using Avalonia.Controls;
using Avalonia.Interactivity;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class LogViewerWindow : Window
{
    public LogViewerWindow() : this(string.Empty)
    {
    }

    public LogViewerWindow(string logDirectory)
    {
        InitializeComponent();
        DataContext = new LogViewerViewModel(logDirectory);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
