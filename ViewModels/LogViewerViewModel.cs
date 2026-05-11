// ViewModels/LogViewerViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Serilog;

namespace ObsidianLauncher.ViewModels;

/// <summary>
///     ViewModel for the Log File Viewer.
/// </summary>
public class LogViewerViewModel : INotifyPropertyChanged
{
    private string _logDirectory = "";
    private string? _selectedLogFile;
    private string _filterText = "";
    private string _logContent = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Available log files in the directory.
    /// </summary>
    public ObservableCollection<string> LogFiles { get; }

    /// <summary>
    ///     Log directory path.
    /// </summary>
    public string LogDirectory
    {
        get => _logDirectory;
        set
        {
            if (_logDirectory != value)
            {
                _logDirectory = value;
                OnPropertyChanged();
                RefreshLogFiles();
            }
        }
    }

    /// <summary>
    ///     Selected log file.
    /// </summary>
    public string? SelectedLogFile
    {
        get => _selectedLogFile;
        set
        {
            if (_selectedLogFile != value)
            {
                _selectedLogFile = value;
                OnPropertyChanged();
                LoadLogFile();
            }
        }
    }

    /// <summary>
    ///     Filter text for searching log content.
    /// </summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText != value)
            {
                _filterText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    /// <summary>
    ///     Log file content (filtered).
    /// </summary>
    public string LogContent
    {
        get => _logContent;
        private set
        {
            if (_logContent != value)
            {
                _logContent = value;
                OnPropertyChanged();
            }
        }
    }

    private string _rawLogContent = "";

    // Commands
    public ICommand RefreshCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CloseCommand { get; }

    public LogViewerViewModel(string logDirectory)
    {
        _logDirectory = logDirectory;
        LogFiles = new ObservableCollection<string>();

        // Initialize commands
        RefreshCommand = new RelayCommand(RefreshLogFiles);
        ClearFilterCommand = new RelayCommand(ClearFilter);
        CopyCommand = new RelayCommand(CopyToClipboard);
        SaveCommand = new RelayCommand(SaveLogFile);
        CloseCommand = new RelayCommand(() => { }); // Dialog handles close

        RefreshLogFiles();
    }

    private void RefreshLogFiles()
    {
        LogFiles.Clear();

        if (!Directory.Exists(LogDirectory))
        {
            Log.Warning("Log directory does not exist: {Directory}", LogDirectory);
            return;
        }

        try
        {
            var files = Directory.GetFiles(LogDirectory, "*.log")
                .Concat(Directory.GetFiles(LogDirectory, "*.txt"))
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>();

            foreach (var file in files)
            {
                LogFiles.Add(file);
            }

            Log.Information("Found {Count} log files", LogFiles.Count);

            // Auto-select first file
            if (LogFiles.Count > 0)
            {
                SelectedLogFile = LogFiles[0];
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading log files from {Directory}", LogDirectory);
        }
    }

    private void LoadLogFile()
    {
        if (string.IsNullOrEmpty(SelectedLogFile))
        {
            _rawLogContent = "";
            LogContent = "";
            return;
        }

        try
        {
            var filePath = Path.Combine(LogDirectory, SelectedLogFile);
            if (!File.Exists(filePath))
            {
                _rawLogContent = "";
                LogContent = "File not found.";
                return;
            }

            // Read log file
            _rawLogContent = File.ReadAllText(filePath);
            Log.Information("Loaded log file: {File} ({Bytes} bytes)", SelectedLogFile, _rawLogContent.Length);

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading log file: {File}", SelectedLogFile);
            LogContent = $"Error reading file: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            LogContent = _rawLogContent;
        }
        else
        {
            // Filter lines containing the search text
            var lines = _rawLogContent.Split('\n')
                .Where(line => line.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
            LogContent = string.Join('\n', lines);
        }
    }

    private void ClearFilter()
    {
        FilterText = "";
    }

    private void CopyToClipboard()
    {
        // TODO: Implement clipboard copy
        Log.Information("Copy to clipboard triggered");
    }

    private void SaveLogFile()
    {
        // TODO: Implement save/export functionality
        Log.Information("Save log file triggered");
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
