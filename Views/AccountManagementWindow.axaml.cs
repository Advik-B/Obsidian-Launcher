// Views/AccountManagementWindow.axaml.cs

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
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
        var dialog = new ContentDialog
        {
            Title = "Not Implemented",
            Content = "Microsoft Account authentication is not yet implemented.\n\nThis feature will be added in a future update.",
            PrimaryButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    private async void OnOfflineUsernameRequested(object? sender, Action<string?> callback)
    {
        var textBox = new TextBox
        {
            Watermark = "Enter username...",
            MaxLength = 16
        };

        var dialog = new ContentDialog
        {
            Title = "Add Offline Account",
            Content = textBox,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel"
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            callback(textBox.Text.Trim());
        else
            callback(null);
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
