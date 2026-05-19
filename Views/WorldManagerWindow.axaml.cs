using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
        var confirmed = false;

        var yesBtn = new Button { Content = "Delete", Classes = { "danger" }, Padding = new Thickness(16, 8) };
        var noBtn  = new Button { Content = "Cancel", Classes = { "ghost" }, Padding = new Thickness(16, 8) };

        var dialog = new Window
        {
            Title = "Delete World",
            Width = 420,
            Height = 210,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Delete world \"{args.WorldName}\"?",
                        FontSize = 18,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "A backup will be created before deletion.",
                        Opacity = 0.7,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { noBtn, yesBtn }
                    }
                }
            }
        };

        yesBtn.Click += (_, _) => { confirmed = true;  dialog.Close(); };
        noBtn.Click  += (_, _) => { confirmed = false; dialog.Close(); };

        await dialog.ShowDialog(this);
        args.Result.SetResult(confirmed);
    }
}
