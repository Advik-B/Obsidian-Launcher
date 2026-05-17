// ViewModels/LogViewerViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class LogViewerViewModel : INotifyPropertyChanged
{
    private const string LiveSessionEntry = "[Live] Current Session";

    private string _logDirectory = "";
    private string? _selectedLogFile;
    private string _filterText = "";
    private string _logContent = "";
    private string _rawLogContent = "";
    private readonly InMemoryLogSink? _liveSink;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<string>? CopyRequested;
    public event EventHandler<string>? SaveRequested;

    public ObservableCollection<string> LogFiles { get; }

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

    public ICommand RefreshCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CloseCommand { get; }

    public LogViewerViewModel(string logDirectory, InMemoryLogSink? liveSink = null)
    {
        _logDirectory = logDirectory;
        _liveSink = liveSink;
        LogFiles = new ObservableCollection<string>();

        RefreshCommand = new RelayCommand(RefreshLogFiles);
        ClearFilterCommand = new RelayCommand(ClearFilter);
        CopyCommand = new RelayCommand(CopyToClipboard);
        SaveCommand = new RelayCommand(SaveLogFile);
        CloseCommand = new RelayCommand(() => { });

        if (_liveSink != null)
        {
            LogFiles.Add(LiveSessionEntry);
            _liveSink.EntryAdded += OnLiveSinkEntryAdded;
        }

        RefreshLogFiles();

        // Auto-select live session when available
        if (_liveSink != null)
            SelectedLogFile = LiveSessionEntry;
    }

    private void OnLiveSinkEntryAdded(string entry)
    {
        if (_selectedLogFile != LiveSessionEntry) return;
        _rawLogContent = string.IsNullOrEmpty(_rawLogContent)
            ? entry
            : _rawLogContent + "\n" + entry;
        ApplyFilter();
    }

    private void RefreshLogFiles()
    {
        // Preserve live entry if present
        var hadLive = LogFiles.Contains(LiveSessionEntry);
        LogFiles.Clear();
        if (hadLive)
            LogFiles.Add(LiveSessionEntry);

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
                LogFiles.Add(file);

            Log.Information("Found {Count} log files", LogFiles.Count);

            // Auto-select first file only when no live sink
            if (_liveSink == null && LogFiles.Count > 0 && SelectedLogFile == null)
                SelectedLogFile = LogFiles[0];
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

        if (SelectedLogFile == LiveSessionEntry && _liveSink != null)
        {
            _rawLogContent = string.Join("\n", _liveSink.GetEntries());
            ApplyFilter();
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
            var lines = _rawLogContent.Split('\n')
                .Where(line => line.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
            LogContent = string.Join('\n', lines);
        }
    }

    private void ClearFilter() => FilterText = "";

    private void CopyToClipboard() => CopyRequested?.Invoke(this, LogContent);

    private void SaveLogFile() => SaveRequested?.Invoke(this, LogContent);

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
