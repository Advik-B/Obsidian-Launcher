using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class CreateInstanceViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<CreateInstanceViewModel>();
    private readonly InstanceManager _instanceManager;
    private readonly VersionService _versionService;
    private readonly Action<Instance>? _onInstanceCreated;
    private readonly Action? _onCancel;
    private readonly Action? _onCreated;
    
    private string _instanceName = "";
    private string _selectedVersion = "";
    private bool _isLoading = false;
    private bool _includeSnapshots = false;

    public ObservableCollection<string> AvailableVersions { get; }
    public ReactiveCommand<Unit, Unit> CreateCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshVersionsCommand { get; }

    public string InstanceName
    {
        get => _instanceName;
        set => SetField(ref _instanceName, value);
    }

    public string SelectedVersion
    {
        get => _selectedVersion;
        set => SetField(ref _selectedVersion, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public bool IncludeSnapshots
    {
        get => _includeSnapshots;
        set
        {
            if (SetField(ref _includeSnapshots, value))
            {
                // Refresh versions when this changes
                RefreshVersions();
            }
        }
    }

    public bool CanCreate => !string.IsNullOrWhiteSpace(InstanceName) && !string.IsNullOrWhiteSpace(SelectedVersion) && !IsLoading;

    public CreateInstanceViewModel(InstanceManager instanceManager, VersionService versionService, Action? onCancel = null, Action? onCreated = null, Action<Instance>? onInstanceCreated = null)
    {
        _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _onCancel = onCancel;
        _onCreated = onCreated;
        _onInstanceCreated = onInstanceCreated;
        
        AvailableVersions = new ObservableCollection<string>();
        
        CreateCommand = ReactiveCommand.Create(CreateInstance, this.WhenAnyValue(x => x.CanCreate));
        CancelCommand = ReactiveCommand.Create(Cancel);
        RefreshVersionsCommand = ReactiveCommand.Create(RefreshVersions);
        
        // Watch for changes to update CanCreate
        this.WhenAnyValue(x => x.InstanceName, x => x.SelectedVersion, x => x.IsLoading)
            .Subscribe(_ => OnPropertyChanged(nameof(CanCreate)));
        
        LoadVersionsAsync();
    }

    private async void CreateInstance()
    {
        if (!CanCreate) return;

        try
        {
            IsLoading = true;
            _logger.Information("Creating instance: {InstanceName} with version {Version}", InstanceName, SelectedVersion);

            var instance = new Instance
            {
                Name = InstanceName,
                MinecraftVersionId = SelectedVersion,
                InstancePath = System.IO.Path.Combine(_instanceManager.GetInstancesDirectory(), InstanceManager.SanitizeName(InstanceName))
            };

            var created = await _instanceManager.CreateInstanceAsync(instance, SelectedVersion);
            if (created)
            {
                _logger.Information("Successfully created instance: {InstanceName}", InstanceName);
                
                // Notify that the instance was created (before setup)
                _onInstanceCreated?.Invoke(instance);
                _onCreated?.Invoke();
            }
            else
            {
                _logger.Error("Failed to create instance: {InstanceName}", InstanceName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating instance: {InstanceName}", InstanceName);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Cancel()
    {
        _logger.Information("Create instance dialog cancelled");
        _onCancel?.Invoke();
    }

    private async void RefreshVersions()
    {
        await LoadVersionsAsync();
    }

    private async Task LoadVersionsAsync()
    {
        try
        {
            IsLoading = true;
            _logger.Information("Loading available Minecraft versions from Mojang API...");
            
            AvailableVersions.Clear();
            
            // Fetch versions dynamically from Mojang API
            var versions = await _versionService.GetVersionsAsync(
                includeReleases: true,
                includeSnapshots: IncludeSnapshots, // Use the toggle value
                includeBetas: false,
                includeAlphas: false,
                limit: IncludeSnapshots ? 50 : 20 // More versions if including snapshots
            );

            foreach (var version in versions)
            {
                AvailableVersions.Add(version);
            }

            // If no versions were fetched, fall back to a few common ones
            if (AvailableVersions.Count == 0)
            {
                _logger.Warning("No versions fetched from API, falling back to common versions");
                var fallbackVersions = new[]
                {
                    "1.21.4",
                    "1.21.3", 
                    "1.21.1",
                    "1.21",
                    "1.20.6",
                    "1.20.4",
                    "1.20.1"
                };

                foreach (var version in fallbackVersions)
                {
                    AvailableVersions.Add(version);
                }
            }

            // Select the first version (latest) by default
            if (AvailableVersions.Count > 0)
            {
                SelectedVersion = AvailableVersions[0];
            }

            _logger.Information("Loaded {Count} Minecraft versions", AvailableVersions.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load Minecraft versions from API, using fallback versions");
            
            // Fallback to common versions if API fails
            AvailableVersions.Clear();
            var fallbackVersions = new[]
            {
                "1.21.4",
                "1.21.3", 
                "1.21.1",
                "1.21",
                "1.20.6",
                "1.20.4",
                "1.20.1",
                "1.19.4",
                "1.19.2",
                "1.18.2",
                "1.17.1",
                "1.16.5"
            };

            foreach (var version in fallbackVersions)
            {
                AvailableVersions.Add(version);
            }

            if (AvailableVersions.Count > 0)
            {
                SelectedVersion = AvailableVersions[0];
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}