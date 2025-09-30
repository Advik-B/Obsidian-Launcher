using System;
using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;
using ObsidianLauncher.Models;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class InstanceSettingsViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<InstanceSettingsViewModel>();
    private readonly Instance _instance;
    
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

    public InstanceSettingsViewModel(Instance instance)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        
        // Load current values
        _instanceName = instance.Name;
        _javaRuntimePath = instance.CustomJavaRuntimePath ?? "";
        _customJvmArguments = new List<string>(instance.CustomJvmArguments);
        _customJvmArgumentsText = string.Join(" ", _customJvmArguments);
        
        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(Cancel);
        BrowseJavaCommand = ReactiveCommand.Create(BrowseJava);
    }

    private void Save()
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
        // TODO: Save instance to disk
    }

    private void Cancel()
    {
        _logger.Information("Instance settings dialog cancelled");
        // TODO: Close dialog without saving
    }

    private void BrowseJava()
    {
        _logger.Information("Browse Java runtime clicked");
        // TODO: Open file dialog to select Java executable
    }
}