// ViewModels/AccountManagementViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class AccountManagementViewModel : INotifyPropertyChanged
{
    private readonly AccountService? _accountService;
    private AccountInfo? _selectedAccount;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? MicrosoftAccountRequested;
    public event EventHandler<Action<string?>>? OfflineUsernameRequested;

    public ObservableCollection<AccountInfo> Accounts { get; }

    public AccountInfo? SelectedAccount
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

    public ICommand AddOfflineAccountCommand { get; }
    public ICommand AddMicrosoftAccountCommand { get; }
    public ICommand RemoveAccountCommand { get; }
    public ICommand SetActiveAccountCommand { get; }
    public ICommand CloseCommand { get; }

    // Designer constructor
    public AccountManagementViewModel()
    {
        Accounts = new ObservableCollection<AccountInfo>();
        AddOfflineAccountCommand = new RelayCommand(AddOfflineAccount);
        AddMicrosoftAccountCommand = new RelayCommand(AddMicrosoftAccount);
        RemoveAccountCommand = new RelayCommand(RemoveAccount, () => SelectedAccount != null);
        SetActiveAccountCommand = new RelayCommand(SetActiveAccount, () => SelectedAccount != null);
        CloseCommand = new RelayCommand(() => { });
    }

    public AccountManagementViewModel(AccountService accountService) : this()
    {
        _accountService = accountService;
        _ = LoadAccountsAsync();
    }

    private async System.Threading.Tasks.Task LoadAccountsAsync()
    {
        if (_accountService == null) return;
        var accounts = await _accountService.LoadAccountsAsync();
        Accounts.Clear();
        foreach (var a in accounts)
            Accounts.Add(a);
        Log.Information("Loaded {Count} accounts", Accounts.Count);
    }

    private void AddOfflineAccount()
    {
        OfflineUsernameRequested?.Invoke(this, async username =>
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            AccountInfo account;
            if (_accountService != null)
                account = await _accountService.AddOfflineAccountAsync(username);
            else
                account = new AccountInfo { Username = username, Type = AccountType.Offline };

            Accounts.Add(account);
            Log.Information("Added offline account: {Username}", username);
        });
    }

    private void AddMicrosoftAccount()
    {
        MicrosoftAccountRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void RemoveAccount()
    {
        if (SelectedAccount == null) return;
        var account = SelectedAccount;
        Accounts.Remove(account);
        SelectedAccount = null;
        if (_accountService != null)
            await _accountService.RemoveAccountAsync(account.Id);
        Log.Information("Removed account: {Username}", account.Username);
    }

    private async void SetActiveAccount()
    {
        if (SelectedAccount == null) return;
        var id = SelectedAccount.Id;
        foreach (var a in Accounts)
            a.IsActive = a.Id == id;
        if (_accountService != null)
            await _accountService.SetActiveAccountAsync(id);
        Log.Information("Set active account: {Username}", SelectedAccount.Username);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
