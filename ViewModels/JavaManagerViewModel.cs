using System;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class JavaManagerViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<JavaManagerViewModel>();
    private readonly JavaManager _javaManager;
    
    public ObservableCollection<JavaRuntimeInfo> JavaRuntimes { get; }
    public ReactiveCommand<Unit, Unit> DownloadJavaCommand { get; }
    public ReactiveCommand<JavaRuntimeInfo, Unit> DeleteJavaCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public JavaManagerViewModel(JavaManager javaManager)
    {
        _javaManager = javaManager ?? throw new ArgumentNullException(nameof(javaManager));
        JavaRuntimes = new ObservableCollection<JavaRuntimeInfo>();
        
        DownloadJavaCommand = ReactiveCommand.Create(DownloadJava);
        DeleteJavaCommand = ReactiveCommand.Create<JavaRuntimeInfo>(DeleteJava);
        RefreshCommand = ReactiveCommand.Create(RefreshJavaRuntimes);
        
        LoadJavaRuntimes();
    }

    private void DownloadJava()
    {
        _logger.Information("Download Java clicked");
        // TODO: Implement Java download dialog with version selection
    }

    private void DeleteJava(JavaRuntimeInfo runtime)
    {
        _logger.Information("Deleting Java runtime: {RuntimePath}", runtime.JavaExecutablePath);
        // TODO: Implement delete logic with confirmation
        JavaRuntimes.Remove(runtime);
    }

    private void RefreshJavaRuntimes()
    {
        _logger.Information("Refreshing Java runtimes...");
        LoadJavaRuntimes();
    }

    private void LoadJavaRuntimes()
    {
        try
        {
            _logger.Information("Loading Java runtimes...");
            var runtimes = _javaManager.GetAvailableRuntimes();
            
            JavaRuntimes.Clear();
            foreach (var runtime in runtimes)
            {
                JavaRuntimes.Add(runtime);
            }
            
            _logger.Information("Loaded {Count} Java runtimes", runtimes.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load Java runtimes: {Message}", ex.Message);
        }
    }
}