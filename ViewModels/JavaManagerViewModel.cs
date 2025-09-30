using System;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using ObsidianLauncher.Models;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class JavaManagerViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<JavaManagerViewModel>();
    
    public ObservableCollection<JavaRuntimeInfo> JavaRuntimes { get; }
    public ReactiveCommand<Unit, Unit> DownloadJavaCommand { get; }
    public ReactiveCommand<JavaRuntimeInfo, Unit> DeleteJavaCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public JavaManagerViewModel()
    {
        JavaRuntimes = new ObservableCollection<JavaRuntimeInfo>();
        
        DownloadJavaCommand = ReactiveCommand.Create(DownloadJava);
        DeleteJavaCommand = ReactiveCommand.Create<JavaRuntimeInfo>(DeleteJava);
        RefreshCommand = ReactiveCommand.Create(RefreshJavaRuntimes);
        
        LoadJavaRuntimes();
    }

    private void DownloadJava()
    {
        _logger.Information("Download Java clicked");
        // TODO: Implement Java download dialog
    }

    private void DeleteJava(JavaRuntimeInfo runtime)
    {
        _logger.Information("Deleting Java runtime: {RuntimePath}", runtime.JavaExecutablePath);
        // TODO: Implement delete logic
    }

    private void RefreshJavaRuntimes()
    {
        _logger.Information("Refreshing Java runtimes...");
        LoadJavaRuntimes();
    }

    private void LoadJavaRuntimes()
    {
        // TODO: Load from JavaManager
        _logger.Information("Loading Java runtimes...");
    }
}