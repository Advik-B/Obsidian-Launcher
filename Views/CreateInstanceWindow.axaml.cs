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
        viewModel.CreationCompleted += (_, _) => Close(viewModel);
    }

    private void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreateInstanceViewModel vm)
            vm.CreateCommand.Execute(null);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
