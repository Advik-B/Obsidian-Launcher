using Avalonia.Controls;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class JavaManagerWindow : Window
{
    public JavaManagerWindow() { InitializeComponent(); }

    public JavaManagerWindow(JavaManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
