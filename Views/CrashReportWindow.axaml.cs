using Avalonia.Controls;
using Avalonia.Input.Platform;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class CrashReportWindow : Window
{
    public CrashReportWindow() { InitializeComponent(); }

    public CrashReportWindow(CrashReportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
        viewModel.CopyRequested += OnCopyRequested;
    }

    private async void OnCopyRequested(object? sender, string content)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
            await clipboard.SetTextAsync(content);
    }
}
