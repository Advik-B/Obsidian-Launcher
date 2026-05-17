using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using ObsidianLauncher.Settings;
using ObsidianLauncher.ViewModels;
using ObsidianLauncher.Views;
using Serilog;

namespace ObsidianLauncher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Log unhandled exceptions on background threads so crashes always produce a log entry
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Fatal(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled domain exception (IsTerminating={IsTerminating})", e.IsTerminating);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var config = new LauncherConfig();
            var settingsPath = System.IO.Path.Combine(config.BaseDataPath, "launcher-settings.toml");
            var settings = new LauncherSettings(settingsPath);

            // Apply theme from settings
            RequestedThemeVariant = settings.Theme.Value?.ToLowerInvariant() switch
            {
                "light" => ThemeVariant.Light,
                "dark"  => ThemeVariant.Dark,
                _       => ThemeVariant.Default
            };

            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            desktop.MainWindow = mainWindow;

            if (!settings.FirstRunCompleted.Value)
            {
                mainWindow.Opened += async (_, _) =>
                {
                    var wizardVm = new SetupWizardViewModel(settings);
                    var wizard = new SetupWizardWindow(wizardVm);
                    await wizard.ShowDialog(mainWindow);
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
