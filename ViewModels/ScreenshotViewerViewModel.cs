// ViewModels/ScreenshotViewerViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Serilog;

namespace ObsidianLauncher.ViewModels;

/// <summary>
///     ViewModel for the Screenshot Viewer/Browser.
/// </summary>
public class ScreenshotViewerViewModel : INotifyPropertyChanged
{
    private string _screenshotDirectory = "";
    private ScreenshotItem? _selectedScreenshot;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Available screenshots.
    /// </summary>
    public ObservableCollection<ScreenshotItem> Screenshots { get; }

    /// <summary>
    ///     Screenshot directory path.
    /// </summary>
    public string ScreenshotDirectory
    {
        get => _screenshotDirectory;
        set
        {
            if (_screenshotDirectory != value)
            {
                _screenshotDirectory = value;
                OnPropertyChanged();
                RefreshScreenshots();
            }
        }
    }

    /// <summary>
    ///     Selected screenshot.
    /// </summary>
    public ScreenshotItem? SelectedScreenshot
    {
        get => _selectedScreenshot;
        set
        {
            if (_selectedScreenshot != value)
            {
                _selectedScreenshot = value;
                OnPropertyChanged();
                ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
                ((RelayCommand)OpenInExplorerCommand).RaiseCanExecuteChanged();
            }
        }
    }

    // Commands
    public ICommand RefreshCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenInExplorerCommand { get; }
    public ICommand CloseCommand { get; }

    public ScreenshotViewerViewModel(string screenshotDirectory)
    {
        _screenshotDirectory = screenshotDirectory;
        Screenshots = new ObservableCollection<ScreenshotItem>();

        // Initialize commands
        RefreshCommand = new RelayCommand(RefreshScreenshots);
        DeleteCommand = new RelayCommand(DeleteScreenshot, () => SelectedScreenshot != null);
        OpenInExplorerCommand = new RelayCommand(OpenInExplorer, () => SelectedScreenshot != null);
        CloseCommand = new RelayCommand(() => { }); // Dialog handles close

        RefreshScreenshots();
    }

    private void RefreshScreenshots()
    {
        Screenshots.Clear();

        if (!Directory.Exists(ScreenshotDirectory))
        {
            Log.Warning("Screenshot directory does not exist: {Directory}", ScreenshotDirectory);
            return;
        }

        try
        {
            var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
            var files = Directory.GetFiles(ScreenshotDirectory)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderByDescending(f => new FileInfo(f).LastWriteTime);

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                Screenshots.Add(new ScreenshotItem
                {
                    FilePath = file,
                    FileName = fileInfo.Name,
                    DateTaken = fileInfo.LastWriteTime,
                    FileSizeBytes = fileInfo.Length
                });
            }

            Log.Information("Found {Count} screenshots", Screenshots.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading screenshots from {Directory}", ScreenshotDirectory);
        }
    }

    private void DeleteScreenshot()
    {
        if (SelectedScreenshot == null) return;

        try
        {
            File.Delete(SelectedScreenshot.FilePath);
            Log.Information("Deleted screenshot: {File}", SelectedScreenshot.FileName);
            RefreshScreenshots();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting screenshot: {File}", SelectedScreenshot.FileName);
        }
    }

    private void OpenInExplorer()
    {
        if (SelectedScreenshot == null) return;

        try
        {
            var folder = Path.GetDirectoryName(SelectedScreenshot.FilePath);
            if (string.IsNullOrEmpty(folder)) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start("explorer.exe", $"/select,\"{SelectedScreenshot.FilePath}\"");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start(new ProcessStartInfo("xdg-open", folder) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start(new ProcessStartInfo("open", $"-R \"{SelectedScreenshot.FilePath}\"") { UseShellExecute = true });

            Log.Information("Opened screenshot in explorer: {File}", SelectedScreenshot.FileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening screenshot in explorer: {File}", SelectedScreenshot.FileName);
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
///     Represents a screenshot item.
/// </summary>
public class ScreenshotItem
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime DateTaken { get; set; }
    public long FileSizeBytes { get; set; }

    public Bitmap? Thumbnail
    {
        get
        {
            if (!File.Exists(FilePath)) return null;
            try { return new Bitmap(FilePath); }
            catch { return null; }
        }
    }

    public string FileSizeDisplay
    {
        get
        {
            if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
            return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }

    public string DateTakenDisplay => DateTaken.ToString("yyyy-MM-dd HH:mm:ss");
}
