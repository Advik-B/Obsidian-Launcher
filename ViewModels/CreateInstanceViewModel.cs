using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class CreateInstanceViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private string _instanceName = "";
    private string _selectedVersionId = "";
    private bool _isCreating;
    private string _errorMessage = "";

    public CreateInstanceViewModel()
    {
        _logger = LogHelper.GetLogger<CreateInstanceViewModel>();
        
        // Initialize commands
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
            {
                ((RelayCommand)CreateCommand).RaiseCanExecuteChanged();
            }
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

    public ICommand SelectVersionCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    public bool Result { get; set; }

    private async Task SelectVersionAsync()
    {
        try
        {
            var window = new Views.VersionSelectorWindow();
            
            // Get parent window for modal dialog
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
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

    private bool CanCreate()
    {
        return !string.IsNullOrWhiteSpace(InstanceName) && 
               !string.IsNullOrWhiteSpace(SelectedVersionId) &&
               !IsCreating;
    }

    private async Task CreateAsync()
    {
        Result = true;
    }
}
