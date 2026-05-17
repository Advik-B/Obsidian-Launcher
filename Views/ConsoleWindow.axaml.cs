// Views/ConsoleWindow.axaml.cs

using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class ConsoleWindow : Window
{
    public ConsoleWindow()
    {
        InitializeComponent();
    }

    public ConsoleWindow(ConsoleViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CopyRequested += OnCopyRequested;
        viewModel.SaveRequested += OnSaveRequested;
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
            Title = "Save Console Log",
            SuggestedFileName = "console.log",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.log", "*.txt" } }
            }
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
        }
    }
}
