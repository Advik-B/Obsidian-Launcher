using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<MainWindowViewModel>();
    private ViewModelBase _currentView;
    private string _title = "Obsidian Launcher";

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
        
        // Initialize with instances view
        _currentView = new InstancesViewModel();
        
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
        CurrentView = new InstancesViewModel();
        Title = "Obsidian Launcher - Instances";
    }

    private void ShowLauncherSettings()
    {
        CurrentView = new LauncherSettingsViewModel();
        Title = "Obsidian Launcher - Settings";
    }

    private void ShowJavaManager()
    {
        CurrentView = new JavaManagerViewModel();
        Title = "Obsidian Launcher - Java Manager";
    }

    private void ShowScreenshots()
    {
        CurrentView = new ScreenshotViewerViewModel();
        Title = "Obsidian Launcher - Screenshots";
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
            // TODO: Load instances from InstanceManager
            _logger.Information("Loading instances...");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load instances");
        }
    }
}