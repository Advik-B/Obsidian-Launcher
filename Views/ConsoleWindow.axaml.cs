// Views/ConsoleWindow.axaml.cs

using Avalonia.Controls;
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
    }
}
