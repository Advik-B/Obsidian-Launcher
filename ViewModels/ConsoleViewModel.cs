// ViewModels/ConsoleViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ObsidianLauncher.Utils;

namespace ObsidianLauncher.ViewModels;

/// <summary>
///     ViewModel for the Console Output Viewer dialog.
/// </summary>
public class ConsoleViewModel : INotifyPropertyChanged
{
    private string _filter = "";
    private string _selectedLogLevel = "All";
    private bool _autoScroll = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     All console output lines.
    /// </summary>
    public ObservableCollection<ConsoleLogEntry> LogEntries { get; }

    /// <summary>
    ///     Filtered console output lines based on filter text and log level.
    /// </summary>
    public ObservableCollection<ConsoleLogEntry> FilteredLogEntries { get; }

    /// <summary>
    ///     Available log levels for filtering.
    /// </summary>
    public ObservableCollection<string> LogLevels { get; }

    /// <summary>
    ///     Filter text for search.
    /// </summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (_filter != value)
            {
                _filter = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    /// <summary>
    ///     Selected log level for filtering.
    /// </summary>
    public string SelectedLogLevel
    {
        get => _selectedLogLevel;
        set
        {
            if (_selectedLogLevel != value)
            {
                _selectedLogLevel = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    /// <summary>
    ///     Auto-scroll to bottom when new log entries are added.
    /// </summary>
    public bool AutoScroll
    {
        get => _autoScroll;
        set
        {
            if (_autoScroll != value)
            {
                _autoScroll = value;
                OnPropertyChanged();
            }
        }
    }

    // Commands
    public ICommand ClearCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand SaveToFileCommand { get; }

    public ConsoleViewModel()
    {
        LogEntries = new ObservableCollection<ConsoleLogEntry>();
        FilteredLogEntries = new ObservableCollection<ConsoleLogEntry>();
        LogLevels = new ObservableCollection<string> { "All", "INFO", "WARN", "ERROR", "DEBUG" };

        ClearCommand = new RelayCommand(Clear);
        CopyCommand = new RelayCommand(CopyToClipboard);
        SaveToFileCommand = new RelayCommand(SaveToFile);

        // Subscribe to collection changes
        LogEntries.CollectionChanged += (s, e) => ApplyFilter();
    }

    /// <summary>
    ///     Adds a new log entry to the console.
    /// </summary>
    public void AddLogEntry(string message, string level = "INFO")
    {
        var entry = new ConsoleLogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        LogEntries.Add(entry);

        // Limit the number of log entries to prevent memory issues
        if (LogEntries.Count > 10000)
        {
            LogEntries.RemoveAt(0);
        }
    }

    private void ApplyFilter()
    {
        FilteredLogEntries.Clear();

        var filtered = LogEntries.AsEnumerable();

        // Filter by log level
        if (SelectedLogLevel != "All")
        {
            filtered = filtered.Where(e => e.Level == SelectedLogLevel);
        }

        // Filter by text
        if (!string.IsNullOrWhiteSpace(Filter))
        {
            filtered = filtered.Where(e =>
                e.Message.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                e.Level.Contains(Filter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var entry in filtered)
        {
            FilteredLogEntries.Add(entry);
        }
    }

    private void Clear()
    {
        LogEntries.Clear();
        FilteredLogEntries.Clear();
    }

    private void CopyToClipboard()
    {
        // TODO: Implement clipboard copy when needed
        // For now, just log the action
        Serilog.Log.Information("Copy to clipboard requested");
    }

    private void SaveToFile()
    {
        // TODO: Implement save to file when needed
        // For now, just log the action
        Serilog.Log.Information("Save to file requested");
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
///     Represents a single console log entry.
/// </summary>
public class ConsoleLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

    public string FormattedMessage => $"[{FormattedTimestamp}] [{Level}] {Message}";
}
