using Avalonia.Controls;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class InstancesView : UserControl
{
    public InstancesView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateEmptyState();
        if (DataContext is MainWindowViewModel vm)
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(MainWindowViewModel.FilteredInstances))
                    UpdateEmptyState();
            };
    }

    private void UpdateEmptyState()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var hasInstances = false;
        foreach (var _ in vm.FilteredInstances) { hasInstances = true; break; }

        var emptyState = this.FindControl<StackPanel>("EmptyState");
        var grid = this.FindControl<ScrollViewer>("InstanceGridScroll");
        if (emptyState != null) emptyState.IsVisible = !hasInstances;
        if (grid != null) grid.IsVisible = hasInstances;
    }
}
