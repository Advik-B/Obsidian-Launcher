using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.BrowseFileRequested += OnBrowseFileRequested;
        viewModel.BrowseFolderRequested += OnBrowseFolderRequested;
    }

    private async void OnBrowseFileRequested(object? sender, (string Title, System.Action<string?> Callback) args)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = args.Title,
            AllowMultiple = false
        });

        args.Callback(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }

    private async void OnBrowseFolderRequested(object? sender, (string Title, System.Action<string?> Callback) args)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = args.Title,
            AllowMultiple = false
        });

        args.Callback(folders.Count > 0 ? folders[0].TryGetLocalPath() : null);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.SaveCommand.Execute(null);
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.CancelCommand.Execute(null);
        Close(false);
    }
}
