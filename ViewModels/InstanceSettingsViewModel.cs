using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class InstanceSettingsViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<InstanceSettingsViewModel>();
    private readonly Instance _instance;
    private readonly InstanceManager? _instanceManager;
    private readonly Action? _onCancel;
    
    private string _instanceName;
    private string _javaRuntimePath;
    private List<string> _customJvmArguments;
    private string _customJvmArgumentsText;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseJavaCommand { get; }

    public string InstanceName
    {
        get => _instanceName;
        set => SetField(ref _instanceName, value);
    }

    public string JavaRuntimePath
    {
        get => _javaRuntimePath;
        set => SetField(ref _javaRuntimePath, value);
    }

    public string CustomJvmArgumentsText
    {
        get => _customJvmArgumentsText;
        set => SetField(ref _customJvmArgumentsText, value);
    }

    public string InstancePath => _instance.InstancePath;
    public string MinecraftVersion => _instance.MinecraftVersionId;
    public string CreatedDate => _instance.CreationDate.ToString("yyyy-MM-dd HH:mm");
    public string LastPlayed => _instance.LastPlayedFormatted;
    public string TotalPlaytime => _instance.TotalPlaytime.ToString(@"d\.hh\:mm\:ss");

    public InstanceSettingsViewModel(Instance instance, InstanceManager? instanceManager = null, Action? onCancel = null)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _instanceManager = instanceManager;
        _onCancel = onCancel;
        
        // Load current values
        _instanceName = instance.Name;
        _javaRuntimePath = instance.CustomJavaRuntimePath ?? "";
        _customJvmArguments = new List<string>(instance.CustomJvmArguments);
        _customJvmArgumentsText = string.Join(" ", _customJvmArguments);
        
        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(Cancel);
        BrowseJavaCommand = ReactiveCommand.Create(BrowseJava);
    }

    private async void Save()
    {
        _instance.Name = InstanceName;
        _instance.CustomJavaRuntimePath = string.IsNullOrWhiteSpace(JavaRuntimePath) ? null : JavaRuntimePath;
        
        // Parse JVM arguments
        _instance.CustomJvmArguments.Clear();
        if (!string.IsNullOrWhiteSpace(CustomJvmArgumentsText))
        {
            var args = CustomJvmArgumentsText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var arg in args)
            {
                _instance.CustomJvmArguments.Add(arg);
            }
        }
        
        _logger.Information("Saved settings for instance: {InstanceName}", InstanceName);
        
        // Save to disk if InstanceManager is available
        if (_instanceManager != null)
        {
            try
            {
                var saved = await _instanceManager.SaveInstanceAsync(_instance);
                if (saved)
                {
                    _logger.Information("Successfully saved instance {InstanceName} to disk", InstanceName);
                }
                else
                {
                    _logger.Error("Failed to save instance {InstanceName} to disk", InstanceName);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving instance {InstanceName} to disk", InstanceName);
            }
        }
        
        // Call cancel to go back (same as closing the dialog)
        Cancel();
    }

    private void Cancel()
    {
        _logger.Information("Instance settings dialog cancelled");
        _onCancel?.Invoke();
    }

    private async void BrowseJava()
    {
        try
        {
            _logger.Information("Browse Java runtime clicked");
            
            var dialog = new Avalonia.Controls.OpenFileDialog
            {
                Title = "Select Java Executable",
                AllowMultiple = false
            };

            // Set filters for Java executables
            dialog.Filters = new List<Avalonia.Controls.FileDialogFilter>
            {
                new() { Name = "Java Executable", Extensions = { "exe" } },
                new() { Name = "All Files", Extensions = { "*" } }
            };

            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && 
                desktop.MainWindow != null)
            {
                var result = await dialog.ShowAsync(desktop.MainWindow);
                if (result != null && result.Length > 0)
                {
                    JavaRuntimePath = result[0];
                    _logger.Information("Selected Java runtime: {JavaPath}", JavaRuntimePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to show Java runtime dialog");
        }
    }
}