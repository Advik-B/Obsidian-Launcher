// Views/ScreenshotViewerWindow.axaml.cs

using Avalonia.Controls;
using Avalonia.Interactivity;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class ScreenshotViewerWindow : Window
{
    public ScreenshotViewerWindow() : this(string.Empty)
    {
    }

    public ScreenshotViewerWindow(string screenshotDirectory)
    {
        InitializeComponent();
        DataContext = new ScreenshotViewerViewModel(screenshotDirectory);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
