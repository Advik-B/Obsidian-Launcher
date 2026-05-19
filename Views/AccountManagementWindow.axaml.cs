// Views/AccountManagementWindow.axaml.cs

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ObsidianLauncher.Services;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class AccountManagementWindow : Window
{
    // Designer constructor
    public AccountManagementWindow()
    {
        InitializeComponent();
        var vm = new AccountManagementViewModel();
        DataContext = vm;
        WireEvents(vm);
    }

    public AccountManagementWindow(LauncherConfig config)
    {
        InitializeComponent();
        var accountService = new AccountService(config);
        var vm = new AccountManagementViewModel(accountService);
        DataContext = vm;
        WireEvents(vm);
    }

    private void WireEvents(AccountManagementViewModel vm)
    {
        vm.MicrosoftAccountRequested += OnMicrosoftAccountRequested;
        vm.OfflineUsernameRequested += OnOfflineUsernameRequested;
    }

    private async void OnMicrosoftAccountRequested(object? sender, EventArgs e)
    {
        var okBtn = new Button { Content = "OK", Padding = new Thickness(16, 8), HorizontalAlignment = HorizontalAlignment.Right };
        var dlg = new Window
        {
            Title = "Not Implemented",
            Width = 380,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Microsoft Account authentication is not yet implemented.\n\nThis feature will be added in a future update.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    okBtn
                }
            }
        };
        okBtn.Click += (_, _) => dlg.Close();
        await dlg.ShowDialog(this);
    }

    private async void OnOfflineUsernameRequested(object? sender, Action<string?> callback)
    {
        var textBox = new TextBox { Watermark = "Enter username...", MaxLength = 16 };
        var okBtn     = new Button { Content = "Add",    Padding = new Thickness(16, 8) };
        var cancelBtn = new Button { Content = "Cancel", Padding = new Thickness(16, 8) };

        string? result = null;
        var dlg = new Window
        {
            Title = "Add Offline Account",
            Width = 360,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Enter a username for the offline account:", FontWeight = FontWeight.SemiBold },
                    textBox,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelBtn, okBtn }
                    }
                }
            }
        };

        okBtn.Click     += (_, _) => { result = textBox.Text; dlg.Close(); };
        cancelBtn.Click += (_, _) => dlg.Close();

        await dlg.ShowDialog(this);

        if (!string.IsNullOrWhiteSpace(result))
            callback(result.Trim());
        else
            callback(null);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
