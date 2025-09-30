using System;
using System.Collections.ObjectModel;
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
    private ScreenshotInfo? _selectedScreenshot;
    
    public ObservableCollection<ScreenshotInfo> Screenshots { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<ScreenshotInfo, Unit> DeleteScreenshotCommand { get; }
    public ReactiveCommand<ScreenshotInfo, Unit> OpenScreenshotCommand { get; }

    public ScreenshotInfo? SelectedScreenshot
    {
        get => _selectedScreenshot;
        set => SetField(ref _selectedScreenshot, value);
    }

    public ScreenshotViewerViewModel()
    {
        Screenshots = new ObservableCollection<ScreenshotInfo>();
        
        RefreshCommand = ReactiveCommand.Create(RefreshScreenshots);
        DeleteScreenshotCommand = ReactiveCommand.Create<ScreenshotInfo>(DeleteScreenshot);
        OpenScreenshotCommand = ReactiveCommand.Create<ScreenshotInfo>(OpenScreenshot);
        
        LoadScreenshots();
    }

    private void RefreshScreenshots()
    {
        _logger.Information("Refreshing screenshots...");
        LoadScreenshots();
    }

    private void DeleteScreenshot(ScreenshotInfo screenshot)
    {
        _logger.Information("Deleting screenshot: {FilePath}", screenshot.FilePath);
        // TODO: Implement delete logic
    }

    private void OpenScreenshot(ScreenshotInfo screenshot)
    {
        _logger.Information("Opening screenshot: {FilePath}", screenshot.FilePath);
        // TODO: Open screenshot in default application
    }

    private void LoadScreenshots()
    {
        // TODO: Scan all instance directories for screenshots
        _logger.Information("Loading screenshots...");
    }
}