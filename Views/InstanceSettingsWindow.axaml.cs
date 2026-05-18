// Views/InstanceSettingsWindow.axaml.cs

using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Settings;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class InstanceSettingsWindow : Window
{
    private Instance? _instance;

    public InstanceSettingsWindow() : this(new Instance { Name = "", InstancePath = "" })
    {
    }

    public InstanceSettingsWindow(Instance instance, LauncherSettings? launcherSettings = null)
    {
        _instance = instance;
        InitializeComponent();
        var vm = new InstanceSettingsViewModel(instance, launcherSettings);
        DataContext = vm;
        vm.BrowseJavaPathRequested += OnBrowseJavaPathRequested;
        vm.PickIconRequested += OnPickIconRequested;
        vm.OpenScreenshotViewerRequested += OnOpenScreenshotViewerRequested;
        vm.OpenWorldManagerRequested += OnOpenWorldManagerRequested;
    }

    private void OnOpenScreenshotViewerRequested(object? sender, System.EventArgs e)
    {
        if (_instance == null) return;
        var screenshotsPath = Path.Combine(_instance.GameDataPath, "screenshots");
        var win = new ScreenshotViewerWindow(screenshotsPath);
        win.Show();
    }

    private void OnOpenWorldManagerRequested(object? sender, System.EventArgs e)
    {
        if (_instance == null) return;
        var resourceManager = new ResourceManager();
        var vm = new WorldManagerViewModel(resourceManager, _instance);
        var win = new WorldManagerWindow(vm);
        win.Show();
    }

    private async void OnPickIconRequested(object? sender, System.Action<string?> callback)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Instance Icon",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.ico", "*.gif" } }
            }
        });

        callback(files.Count > 0 ? files[0].TryGetLocalPath() : null);
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
