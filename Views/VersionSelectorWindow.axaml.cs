// Views/VersionSelectorWindow.axaml.cs

using Avalonia.Controls;
using Avalonia.Interactivity;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class VersionSelectorWindow : Window
{
    public string? SelectedVersion { get; private set; }

    public VersionSelectorWindow()
    {
        InitializeComponent();
        DataContext = new VersionSelectorViewModel();
    }

    private void SelectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VersionSelectorViewModel vm && vm.SelectedVersion != null)
        {
            SelectedVersion = vm.SelectedVersion;
            Close(SelectedVersion);
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
