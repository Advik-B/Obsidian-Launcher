using Avalonia.Controls;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class CreateInstanceWindow : Window
{
    public CreateInstanceWindow()
    {
        InitializeComponent();
    }

    public CreateInstanceWindow(CreateInstanceViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}