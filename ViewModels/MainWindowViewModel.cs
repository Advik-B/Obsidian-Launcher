using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<MainWindowViewModel>();
    private ViewModelBase _currentView;
    private string _title = "Obsidian Launcher";
    
    // Service instances
    private readonly LauncherConfig _launcherConfig;
    private readonly HttpManager _httpManager;
    private readonly JavaManager _javaManager;
    private readonly AssetManager _assetManager;
    private readonly LibraryManager _libraryManager;
    private readonly InstanceManager _instanceManager;

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => SetField(ref _currentView, value);
    }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public ObservableCollection<Instance> Instances { get; }
    public ReactiveCommand<Unit, Unit> ShowInstancesCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowLauncherSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowJavaManagerCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowScreenshotsCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateInstanceCommand { get; }

    public MainWindowViewModel()
    {
        Instances = new ObservableCollection<Instance>();
        
        // Initialize services
        _launcherConfig = new LauncherConfig();
        _httpManager = new HttpManager();
        _javaManager = new JavaManager(_launcherConfig, _httpManager);
        _assetManager = new AssetManager(_launcherConfig, _httpManager);
        _libraryManager = new LibraryManager(_launcherConfig, _httpManager);
        _instanceManager = new InstanceManager(_launcherConfig, _assetManager, _libraryManager);
        
        // Initialize with instances view that has access to services
        _currentView = new InstancesViewModel(_instanceManager, ShowInstanceSettings);
        
        // Setup commands
        ShowInstancesCommand = ReactiveCommand.Create(ShowInstances);
        ShowLauncherSettingsCommand = ReactiveCommand.Create(ShowLauncherSettings);
        ShowJavaManagerCommand = ReactiveCommand.Create(ShowJavaManager);
        ShowScreenshotsCommand = ReactiveCommand.Create(ShowScreenshots);
        CreateInstanceCommand = ReactiveCommand.Create(CreateInstance);

        LoadInstancesAsync();
    }

    private void ShowInstances()
    {
        CurrentView = new InstancesViewModel(_instanceManager, ShowInstanceSettings);
        Title = "Obsidian Launcher - Instances";
    }

    private void ShowLauncherSettings()
    {
        CurrentView = new LauncherSettingsViewModel(_launcherConfig);
        Title = "Obsidian Launcher - Settings";
    }

    private void ShowJavaManager()
    {
        CurrentView = new JavaManagerViewModel(_javaManager);
        Title = "Obsidian Launcher - Java Manager";
    }

    private void ShowScreenshots()
    {
        CurrentView = new ScreenshotViewerViewModel(_launcherConfig);
        Title = "Obsidian Launcher - Screenshots";
    }

    public void ShowInstanceSettings(Instance instance)
    {
        CurrentView = new InstanceSettingsViewModel(instance, _instanceManager, () => ShowInstances());
        Title = $"Obsidian Launcher - {instance.Name} Settings";
    }

    private void CreateInstance()
    {
        // TODO: Implement instance creation dialog
        _logger.Information("Create instance clicked");
    }

    private async void LoadInstancesAsync()
    {
        try
        {
            _logger.Information("Loading instances...");
            var instances = await _instanceManager.GetAllInstancesAsync();
            
            Instances.Clear();
            foreach (var instance in instances)
            {
                Instances.Add(instance);
            }
            
            _logger.Information("Loaded {Count} instances", instances.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load instances");
        }
    }

    public void Dispose()
    {
        _httpManager?.Dispose();
    }
}