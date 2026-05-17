// Views/InstanceSettingsWindow.axaml.cs

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ObsidianLauncher.Models;
using ObsidianLauncher.Settings;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class InstanceSettingsWindow : Window
{
    public InstanceSettingsWindow() : this(new Instance { Name = "", InstancePath = "" })
    {
    }

    public InstanceSettingsWindow(Instance instance, LauncherSettings? launcherSettings = null)
    {
        InitializeComponent();
        var vm = new InstanceSettingsViewModel(instance, launcherSettings);
        DataContext = vm;
        vm.BrowseJavaPathRequested += OnBrowseJavaPathRequested;
    }

    private async void OnBrowseJavaPathRequested(object? sender, System.Action<string?> callback)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Java Executable",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executable") { Patterns = new[] { "java", "java.exe", "javaw.exe", "*" } }
            }
        });

        callback(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstanceSettingsViewModel vm)
        {
            vm.SaveCommand.Execute(null);
            Close(true);
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
