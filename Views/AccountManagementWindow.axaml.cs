// Views/AccountManagementWindow.axaml.cs

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using ObsidianLauncher.ViewModels;

namespace ObsidianLauncher.Views;

public partial class AccountManagementWindow : Window
{
    public AccountManagementWindow()
    {
        InitializeComponent();
        var vm = new AccountManagementViewModel();
        DataContext = vm;

        // Subscribe to Microsoft Account request event
        vm.MicrosoftAccountRequested += OnMicrosoftAccountRequested;
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

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
