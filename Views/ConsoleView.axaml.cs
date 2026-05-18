using System.Collections.ObjectModel;
using Avalonia.Controls;
using ObsidianLauncher.Utils;

namespace ObsidianLauncher.Views;

public partial class ConsoleView : UserControl
{
    private readonly ObservableCollection<string> _logLines = new();

    public ConsoleView()
    {
        InitializeComponent();

        if (this.FindControl<ItemsControl>("LogLines") is { } lc)
            lc.ItemsSource = _logLines;

        // Populate existing entries
        foreach (var entry in InMemoryLogSink.Instance.GetEntries())
            _logLines.Add(entry);

        // Subscribe to new entries
        InMemoryLogSink.Instance.EntryAdded += OnEntryAdded;
    }

    private void OnEntryAdded(string line)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _logLines.Add(line);
            if (_logLines.Count > 2000)
                _logLines.RemoveAt(0);

            // Auto-scroll
            if (this.FindControl<ScrollViewer>("LogScroller") is { } sv)
                sv.ScrollToEnd();
        });
    }
}
