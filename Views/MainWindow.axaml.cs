using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
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
        {
            vm.ConfirmDeleteRequested += OnConfirmDeleteRequested;
            vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsDarkMode) && DataContext is MainWindowViewModel vm)
        {
            Avalonia.Application.Current!.RequestedThemeVariant =
                vm.IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    private async void OnConfirmDeleteRequested(object? sender, ConfirmDeleteEventArgs args)
    {
        // Simple dialog replacement for FluentAvalonia ContentDialog
        var confirmed = false;

        var yesBtn = new Button { Content = "Delete", Classes = { "danger" }, Padding = new Thickness(16, 8) };
        var noBtn  = new Button { Content = "Cancel", Classes = { "ghost" }, Padding = new Thickness(16, 8) };

        var dialog = new Window
        {
            Title = "Delete Instance",
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#eff1f5")),
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Delete \"{args.InstanceName}\"?",
                        FontSize = 18,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = "A backup will be created before deletion.",
                        Opacity = 0.7,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { noBtn, yesBtn }
                    }
                }
            }
        };

        yesBtn.Click += (_, _) => { confirmed = true; dialog.Close(); };
        noBtn.Click  += (_, _) => { confirmed = false; dialog.Close(); };

        await dialog.ShowDialog(this);
        args.Result.SetResult(confirmed);
    }

    private void AccountChip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.NavigateToAccountsCommand.Execute(null);
    }
}
