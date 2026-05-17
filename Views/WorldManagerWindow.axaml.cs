using Avalonia.Controls;
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
    }
}
