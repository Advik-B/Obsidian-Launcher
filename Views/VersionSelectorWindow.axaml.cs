// Views/VersionSelectorWindow.axaml.cs

using Avalonia.Controls;
using Avalonia.Interactivity;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class VersionSelectorWindow : Window
{
    public string? SelectedVersion { get; private set; }

    public VersionSelectorWindow() : this(showSnapshots: true, showOldAlpha: false, showOldBeta: false) { }

    public VersionSelectorWindow(bool showSnapshots, bool showOldAlpha, bool showOldBeta)
    {
        InitializeComponent();
        DataContext = new VersionSelectorViewModel(showSnapshots, showOldAlpha, showOldBeta);
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
