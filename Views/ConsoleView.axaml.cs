using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class ConsoleView : UserControl
{
    private bool _isPaused;

    public ConsoleView()
    {
        InitializeComponent();

        PauseBtn.Click += (_, _) =>
        {
            _isPaused = !_isPaused;
            if (DataContext is MainWindowViewModel vm)
                vm.IsConsolePaused = _isPaused;
        };

        ExportLogBtn.Click += async (_, _) => await ExportLogAsync();
    }

    private async System.Threading.Tasks.Task ExportLogAsync()
    {
        if (DataContext is not MainWindowViewModel vm) return;
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

        var lines = vm.GameOutputLines.ToList();
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        foreach (var line in lines)
            await writer.WriteLineAsync(line);
    }
}
