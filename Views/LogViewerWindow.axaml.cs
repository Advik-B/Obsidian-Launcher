// Views/LogViewerWindow.axaml.cs

using System.IO;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ObsidianLauncher.Utils;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class LogViewerWindow : Window
{
    public LogViewerWindow() : this(string.Empty)
    {
    }

    public LogViewerWindow(string logDirectory, InMemoryLogSink? liveSink = null)
    {
        InitializeComponent();
        var vm = new LogViewerViewModel(logDirectory, liveSink);
        DataContext = vm;
        vm.CopyRequested += OnCopyRequested;
        vm.SaveRequested += OnSaveRequested;
    }

    private async void OnCopyRequested(object? sender, string content)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
            await clipboard.SetTextAsync(content);
    }

    private async void OnSaveRequested(object? sender, string content)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Log",
            SuggestedFileName = "launcher.log",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Log Files") { Patterns = new[] { "*.log", "*.txt" } }
            }
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
