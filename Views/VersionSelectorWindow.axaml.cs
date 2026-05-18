// Views/VersionSelectorWindow.axaml.cs

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ObsidianLauncher.Services;
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
        SubscribeToVm();
    }

    public VersionSelectorWindow(HttpManager httpManager, bool showSnapshots = true, bool showOldAlpha = false, bool showOldBeta = false)
    {
        InitializeComponent();
        DataContext = new VersionSelectorViewModel(httpManager, showSnapshots, showOldAlpha, showOldBeta);
        SubscribeToVm();
    }

    private void SubscribeToVm()
    {
        if (DataContext is VersionSelectorViewModel vm)
            vm.SelectRequested += (_, _) => TryConfirmSelection();
    }

    private void TryConfirmSelection()
    {
        if (DataContext is VersionSelectorViewModel vm && vm.SelectedVersionEntry != null)
        {
            SelectedVersion = vm.SelectedVersionEntry.Id;
            Close(SelectedVersion);
        }
    }

    private void SelectButton_Click(object? sender, RoutedEventArgs e) => TryConfirmSelection();

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void VersionList_DoubleTapped(object? sender, TappedEventArgs e) => TryConfirmSelection();
}
