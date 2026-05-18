using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class InstanceSettingsOverlay : UserControl
{
    public InstanceSettingsOverlay()
    {
        InitializeComponent();

        if (this.FindControl<Button>("BrowseJavaBtn") is { } btn)
            btn.Click += async (_, _) => await BrowseJavaAsync();
    }

    private async Task BrowseJavaAsync()
    {
        if (DataContext is not MainWindowViewModel mainVm) return;
        if (mainVm.CurrentInstanceSettingsVm == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Java Executable",
            AllowMultiple = false
        });

        if (files.Count > 0)
            mainVm.CurrentInstanceSettingsVm.JavaPath = files[0].Path.LocalPath;
    }
}
