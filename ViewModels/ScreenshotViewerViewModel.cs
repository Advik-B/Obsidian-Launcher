using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class ScreenshotInfo
{
    public string FilePath { get; set; } = "";
    public string InstanceName { get; set; } = "";
    public DateTime DateTaken { get; set; }
    public string DisplayName => $"{InstanceName} - {DateTaken:yyyy-MM-dd HH:mm}";
}

public class ScreenshotViewerViewModel : ViewModelBase
{
    private readonly ILogger _logger = Log.ForContext<ScreenshotViewerViewModel>();
    private readonly LauncherConfig _launcherConfig;
    private ScreenshotInfo? _selectedScreenshot;
    
    public ObservableCollection<ScreenshotInfo> Screenshots { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<ScreenshotInfo, Unit> DeleteScreenshotCommand { get; }
    public ReactiveCommand<ScreenshotInfo, Unit> OpenScreenshotCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenScreenshotsFolderCommand { get; }

    public ScreenshotInfo? SelectedScreenshot
    {
        get => _selectedScreenshot;
        set => SetField(ref _selectedScreenshot, value);
    }

    public ScreenshotViewerViewModel(LauncherConfig launcherConfig)
    {
        _launcherConfig = launcherConfig ?? throw new ArgumentNullException(nameof(launcherConfig));
        Screenshots = new ObservableCollection<ScreenshotInfo>();
        
        RefreshCommand = ReactiveCommand.Create(RefreshScreenshots);
        DeleteScreenshotCommand = ReactiveCommand.Create<ScreenshotInfo>(DeleteScreenshot);
        OpenScreenshotCommand = ReactiveCommand.Create<ScreenshotInfo>(OpenScreenshot);
        OpenScreenshotsFolderCommand = ReactiveCommand.Create(OpenScreenshotsFolder);
        
        LoadScreenshots();
    }

    private void RefreshScreenshots()
    {
        _logger.Information("Refreshing screenshots...");
        LoadScreenshots();
    }

    private void DeleteScreenshot(ScreenshotInfo screenshot)
    {
        try
        {
            if (File.Exists(screenshot.FilePath))
            {
                File.Delete(screenshot.FilePath);
                Screenshots.Remove(screenshot);
                _logger.Information("Deleted screenshot: {FilePath}", screenshot.FilePath);
                
                if (SelectedScreenshot == screenshot)
                {
                    SelectedScreenshot = null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete screenshot: {FilePath}", screenshot.FilePath);
        }
    }

    private void OpenScreenshot(ScreenshotInfo screenshot)
    {
        try
        {
            _logger.Information("Opening screenshot: {FilePath}", screenshot.FilePath);
            // TODO: Use Process.Start to open with default application
            // System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(screenshot.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open screenshot: {FilePath}", screenshot.FilePath);
        }
    }

    private void OpenScreenshotsFolder()
    {
        try
        {
            // Find the first instance with screenshots or use base path
            var firstInstance = Directory.Exists(_launcherConfig.InstancesRootDir) 
                ? Directory.GetDirectories(_launcherConfig.InstancesRootDir).FirstOrDefault()
                : _launcherConfig.BaseDataPath;
                
            if (firstInstance != null)
            {
                var screenshotsPath = Path.Combine(firstInstance, "screenshots");
                if (Directory.Exists(screenshotsPath))
                {
                    // TODO: Use Process.Start to open folder
                    _logger.Information("Opening screenshots folder: {Path}", screenshotsPath);
                    return;
                }
            }
            
            _logger.Warning("No screenshots folder found");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open screenshots folder");
        }
    }

    private void LoadScreenshots()
    {
        try
        {
            _logger.Information("Loading screenshots...");
            Screenshots.Clear();
            
            // Check if instances directory exists
            if (!Directory.Exists(_launcherConfig.InstancesRootDir))
            {
                _logger.Warning("Instances directory does not exist: {Path}", _launcherConfig.InstancesRootDir);
                return;
            }
            
            // Scan all instance directories for screenshots
            var instanceDirs = Directory.GetDirectories(_launcherConfig.InstancesRootDir);
            foreach (var instanceDir in instanceDirs)
            {
                var instanceName = Path.GetFileName(instanceDir);
                var screenshotsDir = Path.Combine(instanceDir, "screenshots");
                
                if (Directory.Exists(screenshotsDir))
                {
                    var imageExtensions = new[] { ".png", ".jpg", ".jpeg" };
                    var screenshots = Directory.GetFiles(screenshotsDir)
                        .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .Select(f => new ScreenshotInfo
                        {
                            FilePath = f,
                            InstanceName = instanceName,
                            DateTaken = File.GetCreationTime(f)
                        })
                        .OrderByDescending(s => s.DateTaken);
                    
                    foreach (var screenshot in screenshots)
                    {
                        Screenshots.Add(screenshot);
                    }
                }
            }
            
            _logger.Information("Loaded {Count} screenshots", Screenshots.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load screenshots: {Message}", ex.Message);
        }
    }
}