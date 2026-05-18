using Avalonia.Controls;
using Avalonia.Input;
using FluentAvalonia.UI.Controls;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.ConfirmDeleteRequested += OnConfirmDeleteRequested;
    }

    private async void OnConfirmDeleteRequested(object? sender, ConfirmDeleteEventArgs args)
    {
        var dialog = new ContentDialog
        {
            Title = "Delete Instance",
            Content = $"Delete \"{args.InstanceName}\"?\n\nA backup will be created before deletion.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel"
        };

        var result = await dialog.ShowAsync();
        args.Result.SetResult(result == ContentDialogResult.Primary);
    }

    private void InstanceList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.LaunchInstanceCommand.Execute(null);
    }
}
