using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ObsidianLauncher.Utils;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class ConsoleView : UserControl
{
    private readonly ObservableCollection<string> _logLines = new();
    private bool _isPaused;

    public ConsoleView()
    {
        InitializeComponent();

        if (this.FindControl<ItemsControl>("LogLines") is { } lc)
            lc.ItemsSource = _logLines;

        foreach (var entry in InMemoryLogSink.Instance.GetEntries())
            _logLines.Add(entry);

        InMemoryLogSink.Instance.EntryAdded += OnEntryAdded;

        if (this.FindControl<Button>("PauseBtn") is { } pauseBtn)
            pauseBtn.Click += (_, _) =>
            {
                _isPaused = !_isPaused;
                if (DataContext is MainWindowViewModel vm)
                    vm.IsConsolePaused = _isPaused;
            };

        if (this.FindControl<Button>("ExportLogBtn") is { } exportBtn)
            exportBtn.Click += async (_, _) => await ExportLogAsync();
    }

    private void OnEntryAdded(string line)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_isPaused) return;

            _logLines.Add(line);
            if (_logLines.Count > 2000)
                _logLines.RemoveAt(0);

            if (this.FindControl<ScrollViewer>("LogScroller") is { } sv)
                sv.ScrollToEnd();
        });
    }

    private async System.Threading.Tasks.Task ExportLogAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Log",
            SuggestedFileName = $"obsidian-log-{System.DateTime.Now:yyyy-MM-dd_HH-mm}.txt",
            DefaultExtension = "txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text file") { Patterns = new[] { "*.txt" } }
            }
        });

        if (file == null) return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        foreach (var line in _logLines)
            await writer.WriteLineAsync(line);
    }
}
