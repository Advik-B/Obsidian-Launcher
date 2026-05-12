using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class CreateInstanceViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private readonly HttpManager? _httpManager;
    private readonly InstanceManager? _instanceManager;

    private string _instanceName = "";
    private string _selectedVersionId = "";
    private bool _isCreating;
    private string _errorMessage = "";
    private double _progressValue;
    private string _progressText = "";

    public event EventHandler? CreationCompleted;

    // Designer constructor
    public CreateInstanceViewModel() : this(null, null) { }

    public CreateInstanceViewModel(HttpManager? httpManager, InstanceManager? instanceManager = null)
    {
        _logger = LogHelper.GetLogger<CreateInstanceViewModel>();
        _httpManager = httpManager;
        _instanceManager = instanceManager;

        SelectVersionCommand = new RelayCommand(async () => await SelectVersionAsync());
        CreateCommand = new RelayCommand(async () => await CreateAsync(), CanCreate);
        CancelCommand = new RelayCommand(() => { });
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
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
        }
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
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    private async Task SelectVersionAsync()
    {
        try
        {
            var window = _httpManager != null
                ? new Views.VersionSelectorWindow(_httpManager)
                : new Views.VersionSelectorWindow();

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

    private bool CanCreate() =>
        !string.IsNullOrWhiteSpace(InstanceName) &&
        !string.IsNullOrWhiteSpace(SelectedVersionId) &&
        !IsCreating;

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
            var components = new List<Component>
            {
                new() { Uid = "net.minecraft", Version = SelectedVersionId, IsImportant = true }
            };

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
}
