using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ObsidianLauncher.Enums;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Services.ModLoaders;
using ObsidianLauncher.Settings;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class CreateInstanceViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private readonly LauncherSettings? _launcherSettings;
    private readonly HttpManager? _httpManager;
    private readonly InstanceManager? _instanceManager;
    private string _instanceName = "";
    private string _selectedVersionId = "";
    private bool _isCreating;
    private string _errorMessage = "";
    private ModLoaderType _selectedModLoader = ModLoaderType.None;
    private string _modLoaderVersion = "";
    private bool _isLoadingLoaderVersions;
    private double _progressValue;
    private string _progressText = "";

    public event EventHandler? CreationCompleted;

    // Designer constructor
    public CreateInstanceViewModel() : this((HttpManager?)null, null) { }

    public CreateInstanceViewModel(HttpManager? httpManager, InstanceManager? instanceManager = null, LauncherSettings? launcherSettings = null)
    {
        _logger = LogHelper.GetLogger<CreateInstanceViewModel>();
        _httpManager = httpManager;
        _instanceManager = instanceManager;
        _launcherSettings = launcherSettings;

        SelectVersionCommand = new RelayCommand(async () => await SelectVersionAsync());
        LoadModLoaderVersionsCommand = new RelayCommand(async () => await LoadModLoaderVersionsAsync(), () => !string.IsNullOrEmpty(SelectedVersionId) && SelectedModLoader != ModLoaderType.None);
        CreateCommand = new RelayCommand(async () => await CreateAsync(), CanCreate);
        CancelCommand = new RelayCommand(() => { });

        ModLoaderVersions = new ObservableCollection<string>();
    }

    public string InstanceName
    {
        get => _instanceName;
        set
        {
            if (SetProperty(ref _instanceName, value))
            {
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
                ErrorMessage = "";
            }
        }
    }

    public string SelectedVersionId
    {
        get => _selectedVersionId;
        set
        {
            if (SetProperty(ref _selectedVersionId, value))
            {
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
                ((RelayCommand)LoadModLoaderVersionsCommand).RaiseCanExecuteChanged();
                // Clear loader versions when MC version changes
                ModLoaderVersions.Clear();
                ModLoaderVersion = "";
            }
        }
    }

    public ModLoaderType SelectedModLoader
    {
        get => _selectedModLoader;
        set
        {
            if (SetProperty(ref _selectedModLoader, value))
            {
                OnPropertyChanged(nameof(ShowModLoaderVersion));
                OnPropertyChanged(nameof(SelectedModLoaderIndex));
                ((RelayCommand)LoadModLoaderVersionsCommand).RaiseCanExecuteChanged();
                ModLoaderVersions.Clear();
                ModLoaderVersion = "";
            }
        }
    }

    public string ModLoaderVersion
    {
        get => _modLoaderVersion;
        set
        {
            if (SetProperty(ref _modLoaderVersion, value))
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
        }
    }

    public bool ShowModLoaderVersion => SelectedModLoader != ModLoaderType.None;

    public int SelectedModLoaderIndex
    {
        get => (int)_selectedModLoader;
        set => SelectedModLoader = (ModLoaderType)value;
    }

    public ObservableCollection<string> ModLoaderVersions { get; }

    public bool IsLoadingLoaderVersions
    {
        get => _isLoadingLoaderVersions;
        set => SetProperty(ref _isLoadingLoaderVersions, value);
    }

    public bool IsCreating
    {
        get => _isCreating;
        set => SetProperty(ref _isCreating, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    /// <summary>The created instance — set on successful creation, null on failure.</summary>
    public Instance? CreatedInstance { get; private set; }

    public ICommand SelectVersionCommand { get; }
    public ICommand LoadModLoaderVersionsCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Returns the component list to use when creating the instance.
    /// </summary>
    public List<Component> BuildComponents()
    {
        var components = new List<Component>
        {
            new() { Uid = "net.minecraft", Version = SelectedVersionId, IsEnabled = true, IsImportant = true }
        };

        if (SelectedModLoader != ModLoaderType.None && !string.IsNullOrEmpty(ModLoaderVersion))
        {
            var loaderVersion = SelectedModLoader switch
            {
                ModLoaderType.Fabric => $"{SelectedVersionId}/{ModLoaderVersion}",
                ModLoaderType.Quilt => $"{SelectedVersionId}/{ModLoaderVersion}",
                ModLoaderType.Forge => ModLoaderVersion,
                ModLoaderType.NeoForge => ModLoaderVersion,
                _ => ModLoaderVersion
            };

            var uid = SelectedModLoader switch
            {
                ModLoaderType.Fabric => "net.fabricmc.fabric-loader",
                ModLoaderType.Quilt => "org.quiltmc.quilt-loader",
                ModLoaderType.Forge => "net.minecraftforge",
                ModLoaderType.NeoForge => "net.neoforged.neoforge",
                _ => null
            };

            if (uid != null)
            {
                components.Add(new Component { Uid = uid, Version = loaderVersion, IsEnabled = true });
            }
        }

        return components;
    }

    private async Task SelectVersionAsync()
    {
        try
        {
            bool showSnapshots = _launcherSettings?.ShowSnapshots.Value ?? true;
            bool showOldAlpha = _launcherSettings?.ShowOldAlpha.Value ?? false;
            bool showOldBeta = _launcherSettings?.ShowOldBeta.Value ?? false;

            var window = _httpManager != null
                ? new Views.VersionSelectorWindow(_httpManager, showSnapshots, showOldAlpha, showOldBeta)
                : new Views.VersionSelectorWindow(showSnapshots, showOldAlpha, showOldBeta);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var result = await window.ShowDialog<string?>(desktop.MainWindow!);
                if (!string.IsNullOrEmpty(result))
                {
                    SelectedVersionId = result;
                    _logger.Information("Selected Minecraft version: {VersionId}", result);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error selecting version");
            ErrorMessage = "Failed to open version selector";
        }
    }

    private async Task LoadModLoaderVersionsAsync()
    {
        if (string.IsNullOrEmpty(SelectedVersionId) || SelectedModLoader == ModLoaderType.None)
            return;

        IsLoadingLoaderVersions = true;
        ModLoaderVersions.Clear();
        ModLoaderVersion = "";

        try
        {
            List<string>? versions = null;
            var httpManager = _httpManager ?? new HttpManager();

            if (SelectedModLoader == ModLoaderType.Fabric)
            {
                var url = $"https://meta.fabricmc.net/v2/versions/loader/{Uri.EscapeDataString(SelectedVersionId)}";
                var resp = await httpManager.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var entries = JsonSerializer.Deserialize<List<FabricLoaderEntry>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    versions = entries?.Select(e => e.Loader?.Version ?? "").Where(v => !string.IsNullOrEmpty(v)).ToList();
                }
            }
            else if (SelectedModLoader == ModLoaderType.Quilt)
            {
                var installer = new QuiltInstaller(httpManager);
                var quiltVersions = await installer.GetLoadersForGameVersionAsync(SelectedVersionId);
                versions = quiltVersions?.Select(e => e.Loader?.Version ?? "").Where(v => !string.IsNullOrEmpty(v)).ToList();
            }
            else if (SelectedModLoader == ModLoaderType.Forge)
            {
                var config = new LauncherConfig();
                var installer = new ForgeInstaller(httpManager, config);
                versions = await installer.GetForgeVersionsAsync(SelectedVersionId);
            }
            else if (SelectedModLoader == ModLoaderType.NeoForge)
            {
                var config = new LauncherConfig();
                var installer = new NeoForgeInstaller(httpManager, config);
                versions = await installer.GetNeoForgeVersionsAsync();
            }

            if (versions != null)
            {
                foreach (var v in versions.Take(50))
                    ModLoaderVersions.Add(v);

                if (ModLoaderVersions.Count > 0)
                    ModLoaderVersion = ModLoaderVersions[0];
            }
            else
            {
                ErrorMessage = "Failed to load mod loader versions. Check your internet connection.";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading mod loader versions");
            ErrorMessage = "Failed to load versions";
        }
        finally
        {
            IsLoadingLoaderVersions = false;
        }
    }

    private bool CanCreate()
    {
        return !string.IsNullOrWhiteSpace(InstanceName) &&
               !string.IsNullOrWhiteSpace(SelectedVersionId) &&
               !IsCreating &&
               (SelectedModLoader == ModLoaderType.None || !string.IsNullOrWhiteSpace(ModLoaderVersion));
    }

    private async Task CreateAsync()
    {
        if (_instanceManager == null)
        {
            // No manager injected (designer mode) — signal done immediately
            CreatedInstance = null;
            CreationCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        IsCreating = true;
        ErrorMessage = "";
        ProgressValue = 0;
        ProgressText = "Preparing...";
        ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();

        try
        {
            var components = BuildComponents();

            var assetProgress = new Progress<AssetDownloadProgress>(report =>
            {
                ProgressValue = report.TotalFiles > 0
                    ? (double)report.ProcessedFiles / report.TotalFiles * 100
                    : 0;
                ProgressText = $"Downloading assets: {report.ProcessedFiles} / {report.TotalFiles}";
            });

            var libraryProgress = new Progress<LibraryProcessingProgress>(report =>
            {
                ProgressValue = report.TotalLibraries > 0
                    ? (double)report.ProcessedLibraries / report.TotalLibraries * 100
                    : 0;
                ProgressText = $"Processing libraries: {report.ProcessedLibraries} / {report.TotalLibraries}";
            });

            _logger.Information("Creating instance: {Name}, Version: {Version}", InstanceName, SelectedVersionId);

            CreatedInstance = await _instanceManager.CreateInstanceAsync(
                InstanceName,
                components,
                assetProgress,
                libraryProgress
            );

            if (CreatedInstance == null)
            {
                ErrorMessage = "Failed to create instance. Check the log viewer for details.";
                _logger.Error("Instance creation returned null for {Name}", InstanceName);
                return;
            }

            ProgressValue = 100;
            ProgressText = "Done!";
            _logger.Information("Instance created: {Name}", InstanceName);
            CreationCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating instance {Name}", InstanceName);
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsCreating = false;
            ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
        }
    }

    // Local model for Fabric loader version list parsing
    private class FabricLoaderEntry
    {
        public FabricLoaderInfo? Loader { get; set; }
    }

    private class FabricLoaderInfo
    {
        public string? Version { get; set; }
        public bool Stable { get; set; }
    }
}
