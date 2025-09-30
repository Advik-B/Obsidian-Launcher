using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Serilog;
using ObsidianLauncher.Services;

namespace ObsidianLauncher.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private bool _isLoading;
    private string _statusText = "Ready";
    private MinecraftInstanceViewModel? _selectedInstance;

    public MainWindowViewModel()
    {
        _logger = Log.ForContext<MainWindowViewModel>();
        
        // Initialize with some sample instances for now
        Instances = new ObservableCollection<MinecraftInstanceViewModel>
        {
            new MinecraftInstanceViewModel("Minecraft 1.20.4", "1.20.4", "Release"),
            new MinecraftInstanceViewModel("Minecraft 1.19.4", "1.19.4", "Release"),
            new MinecraftInstanceViewModel("Minecraft 1.18.2", "1.18.2", "Release")
        };

        LaunchCommand = ReactiveCommand.CreateFromTask(LaunchSelectedInstance, this.WhenAnyValue(x => x.SelectedInstance).Select(instance => instance != null));
        CreateInstanceCommand = ReactiveCommand.Create(CreateNewInstance);
        DeleteInstanceCommand = ReactiveCommand.Create(DeleteSelectedInstance, this.WhenAnyValue(x => x.SelectedInstance).Select(instance => instance != null));
        SettingsCommand = ReactiveCommand.Create(OpenSettings);
    }

    public ObservableCollection<MinecraftInstanceViewModel> Instances { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public MinecraftInstanceViewModel? SelectedInstance
    {
        get => _selectedInstance;
        set => this.RaiseAndSetIfChanged(ref _selectedInstance, value);
    }

    public ReactiveCommand<Unit, Unit> LaunchCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateInstanceCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteInstanceCommand { get; }
    public ReactiveCommand<Unit, Unit> SettingsCommand { get; }

    private async Task LaunchSelectedInstance()
    {
        if (SelectedInstance == null) return;

        try
        {
            IsLoading = true;
            StatusText = $"Launching {SelectedInstance.Name}...";
            _logger.Information("Starting launch of instance: {InstanceName}", SelectedInstance.Name);

            // For now, just simulate launching
            await Task.Delay(2000);
            
            StatusText = $"Launched {SelectedInstance.Name}";
            _logger.Information("Successfully launched instance: {InstanceName}", SelectedInstance.Name);
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to launch {SelectedInstance.Name}";
            _logger.Error(ex, "Failed to launch instance: {InstanceName}", SelectedInstance.Name);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CreateNewInstance()
    {
        _logger.Information("Create new instance requested");
        StatusText = "Creating new instance...";
        // TODO: Implement instance creation dialog
    }

    private void DeleteSelectedInstance()
    {
        if (SelectedInstance == null) return;

        _logger.Information("Delete instance requested: {InstanceName}", SelectedInstance.Name);
        StatusText = $"Deleting {SelectedInstance.Name}...";
        // TODO: Implement instance deletion with confirmation
    }

    private void OpenSettings()
    {
        _logger.Information("Settings dialog requested");
        StatusText = "Opening settings...";
        // TODO: Implement settings dialog
    }
}