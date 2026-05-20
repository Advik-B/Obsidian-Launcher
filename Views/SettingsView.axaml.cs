using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        BrowseGlobalJavaBtn.Click += async (_, _) => await BrowseJavaAsync();
    }

    private async Task BrowseJavaAsync()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Java Executable",
            AllowMultiple = false
        });

        if (files.Count > 0)
            vm.GlobalJavaPath = files[0].Path.LocalPath;
    }
}
