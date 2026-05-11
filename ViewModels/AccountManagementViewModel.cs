// ViewModels/AccountManagementViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Serilog;

namespace ObsidianLauncher.ViewModels;

/// <summary>
///     ViewModel for the Account Management dialog.
/// </summary>
public class AccountManagementViewModel : INotifyPropertyChanged
{
    private string? _selectedAccount;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     List of all accounts.
    /// </summary>
    public ObservableCollection<string> Accounts { get; }

    /// <summary>
    ///     Selected account.
    /// </summary>
    public string? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (_selectedAccount != value)
            {
                _selectedAccount = value;
                OnPropertyChanged();
                ((RelayCommand)RemoveAccountCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SetActiveAccountCommand).RaiseCanExecuteChanged();
            }
        }
    }

    // Commands
    public ICommand AddOfflineAccountCommand { get; }
    public ICommand AddMicrosoftAccountCommand { get; }
    public ICommand RemoveAccountCommand { get; }
    public ICommand SetActiveAccountCommand { get; }
    public ICommand CloseCommand { get; }

    public AccountManagementViewModel()
    {
        Accounts = new ObservableCollection<string>();

        // Initialize commands
        AddOfflineAccountCommand = new RelayCommand(AddOfflineAccount);
        AddMicrosoftAccountCommand = new RelayCommand(AddMicrosoftAccount);
        RemoveAccountCommand = new RelayCommand(RemoveAccount, () => SelectedAccount != null);
        SetActiveAccountCommand = new RelayCommand(SetActiveAccount, () => SelectedAccount != null);
        CloseCommand = new RelayCommand(() => { }); // Dialog handles close

        // TODO: Load accounts from persistent storage
        LoadAccounts();
    }

    private void LoadAccounts()
    {
        // TODO: Load accounts from file/settings
        // For now, just add a placeholder
        Log.Information("Loading accounts...");
    }

    private void AddOfflineAccount()
    {
        Log.Information("Add Offline Account triggered");
        // TODO: Show dialog to enter offline username
        // For now, add a test account
        var username = "OfflinePlayer_" + (Accounts.Count + 1);
        Accounts.Add(username);
        Log.Information("Added offline account: {Username}", username);
    }

    private void AddMicrosoftAccount()
    {
        Log.Information("Add Microsoft Account triggered");
        // Trigger event to show not implemented dialog
        MicrosoftAccountRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Event raised when user tries to add a Microsoft account.
    /// </summary>
    public event EventHandler? MicrosoftAccountRequested;

    private void RemoveAccount()
    {
        if (SelectedAccount != null)
        {
            Log.Information("Removing account: {Account}", SelectedAccount);
            Accounts.Remove(SelectedAccount);
            SelectedAccount = null;
        }
    }

    private void SetActiveAccount()
    {
        if (SelectedAccount != null)
        {
            Log.Information("Setting active account: {Account}", SelectedAccount);
            // TODO: Save active account to settings
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
