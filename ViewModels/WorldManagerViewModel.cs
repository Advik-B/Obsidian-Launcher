using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class WorldManagerViewModel : ViewModelBase
{
    private readonly ResourceManager _resourceManager;
    private readonly Instance _instance;
    private readonly ILogger _logger;

    private ResourceItem? _selectedWorld;
    private bool _isLoading;
    private string _statusText = "";

    public WorldManagerViewModel(ResourceManager resourceManager, Instance instance)
    {
        _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _logger = LogHelper.GetLogger<WorldManagerViewModel>();

        Worlds = new ObservableCollection<ResourceItem>();

        RefreshCommand = new RelayCommand(async () => await LoadWorldsAsync());
        OpenFolderCommand = new RelayCommand(OpenFolder, () => SelectedWorld != null);
        ExportCommand = new RelayCommand(async () => await ExportWorldAsync(), () => SelectedWorld != null);
        DeleteCommand = new RelayCommand(async () => await DeleteWorldAsync(), () => SelectedWorld != null);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

        _ = LoadWorldsAsync();
    }

    public ObservableCollection<ResourceItem> Worlds { get; }

    public ResourceItem? SelectedWorld
    {
        get => _selectedWorld;
        set
        {
            if (SetProperty(ref _selectedWorld, value))
            {
                ((RelayCommand)OpenFolderCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CloseCommand { get; }

    public event EventHandler? CloseRequested;

    private async Task LoadWorldsAsync()
    {
        IsLoading = true;
        Worlds.Clear();
        StatusText = "Loading worlds...";

        try
        {
            var worlds = await _resourceManager.ListResourcesAsync(_instance, ResourceFolderType.Worlds);
            foreach (var world in worlds)
                Worlds.Add(world);

            StatusText = $"{Worlds.Count} world(s) found";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load worlds");
            StatusText = "Error loading worlds";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenFolder()
    {
        if (SelectedWorld == null) return;
        var path = Directory.Exists(SelectedWorld.Path) ? SelectedWorld.Path : Path.GetDirectoryName(SelectedWorld.Path);
        if (path == null) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start("explorer.exe", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", path);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open folder {Path}", path);
        }
    }

    private async Task ExportWorldAsync()
    {
        if (SelectedWorld == null) return;

        var destPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"{SelectedWorld.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        StatusText = $"Exporting {SelectedWorld.Name}...";
        var success = await _resourceManager.ExportWorldAsync(_instance, SelectedWorld.Name, destPath);
        StatusText = success ? $"Exported to {destPath}" : "Export failed";
    }

    private async Task DeleteWorldAsync()
    {
        if (SelectedWorld == null) return;

        _resourceManager.DeleteResource(SelectedWorld, createBackup: true);
        Worlds.Remove(SelectedWorld);
        SelectedWorld = null;
        StatusText = "World deleted (backup created)";
    }
}
