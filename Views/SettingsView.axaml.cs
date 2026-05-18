using Avalonia.Controls;
using Avalonia.Styling;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        if (this.FindControl<Button>("LightThemeBtn") is { } lightBtn)
            lightBtn.Click += (_, _) =>
            {
                Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
                if (DataContext is MainWindowViewModel vm) vm.IsDarkMode = false;
            };

        if (this.FindControl<Button>("DarkThemeBtn") is { } darkBtn)
            darkBtn.Click += (_, _) =>
            {
                Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
                if (DataContext is MainWindowViewModel vm) vm.IsDarkMode = true;
            };
    }
}
