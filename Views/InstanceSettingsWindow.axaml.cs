// Views/InstanceSettingsWindow.axaml.cs

using Avalonia.Controls;
using Avalonia.Interactivity;
using ObsidianLauncher.Models;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class InstanceSettingsWindow : Window
{
    public InstanceSettingsWindow() : this(new Instance { Name = "", InstancePath = "" })
    {
    }

    public InstanceSettingsWindow(Instance instance)
    {
        InitializeComponent();
        DataContext = new InstanceSettingsViewModel(instance);
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstanceSettingsViewModel vm)
        {
            vm.SaveCommand.Execute(null);
            Close(true); // Return true to indicate save
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false); // Return false to indicate cancel
    }
}
