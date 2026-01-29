using Avalonia.Controls;
using Avalonia.Interactivity;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class CreateInstanceWindow : Window
{
    public CreateInstanceWindow()
    {
        InitializeComponent();
        DataContext = new CreateInstanceViewModel();
    }

    public CreateInstanceWindow(CreateInstanceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreateInstanceViewModel vm)
        {
            vm.Result = true;
        }
        Close(DataContext);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreateInstanceViewModel vm)
        {
            vm.Result = false;
        }
        Close(null);
    }
}
