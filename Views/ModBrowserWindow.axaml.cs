using Avalonia.Controls;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services.Modrinth;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class ModBrowserWindow : Window
{
    public ModBrowserWindow(ModManager modManager, Instance instance)
    {
        InitializeComponent();
        DataContext = new ModBrowserViewModel(modManager, instance);
    }

    public ModBrowserWindow()
    {
        InitializeComponent();
    }
}
