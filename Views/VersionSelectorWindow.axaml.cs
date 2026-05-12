// Views/VersionSelectorWindow.axaml.cs

using Avalonia.Controls;
using Avalonia.Interactivity;
using ObsidianLauncher.Services;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class VersionSelectorWindow : Window
{
    // Designer / no-network constructor
    public VersionSelectorWindow()
    {
        InitializeComponent();
        DataContext = new VersionSelectorViewModel();
    }

    public VersionSelectorWindow(HttpManager httpManager)
    {
        InitializeComponent();
        DataContext = new VersionSelectorViewModel(httpManager);
    }

    private void SelectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VersionSelectorViewModel vm && vm.SelectedVersion != null)
            Close(vm.SelectedVersion.Id);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
