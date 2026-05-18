using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class WorldManagerWindow : Window
{
    public WorldManagerWindow() { InitializeComponent(); }

    public WorldManagerWindow(WorldManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
        viewModel.ConfirmDeleteRequested += OnConfirmDeleteRequested;
    }

    private async void OnConfirmDeleteRequested(object? sender, ConfirmWorldDeleteEventArgs args)
    {
        var dialog = new ContentDialog
        {
            Title = "Delete World",
            Content = $"Delete world \"{args.WorldName}\"?\n\nA backup will be created before deletion.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel"
        };

        var result = await dialog.ShowAsync();
        args.Result.SetResult(result == ContentDialogResult.Primary);
    }
}
