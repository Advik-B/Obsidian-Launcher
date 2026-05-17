using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

public class AccountService
{
    private readonly string _accountsFilePath;
    private readonly ILogger _logger;

    public AccountService(LauncherConfig config)
    {
        _accountsFilePath = Path.Combine(config.DataRootDir, "accounts.json");
        _logger = LogHelper.GetLogger<AccountService>();
    }

    public async Task<List<AccountInfo>> LoadAccountsAsync()
    {
        if (!File.Exists(_accountsFilePath))
            return new List<AccountInfo>();

        try
        {
            var json = await File.ReadAllTextAsync(_accountsFilePath);
            var accounts = JsonSerializer.Deserialize<List<AccountInfo>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return accounts ?? new List<AccountInfo>();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load accounts from {Path}", _accountsFilePath);
            return new List<AccountInfo>();
        }
    }

    public async Task SaveAccountsAsync(List<AccountInfo> accounts)
    {
        try
        {
            var json = JsonSerializer.Serialize(accounts, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_accountsFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save accounts to {Path}", _accountsFilePath);
        }
    }

    public async Task<AccountInfo> AddOfflineAccountAsync(string username)
    {
        var accounts = await LoadAccountsAsync();
        var account = new AccountInfo { Username = username, Type = AccountType.Offline };
        accounts.Add(account);
        await SaveAccountsAsync(accounts);
        _logger.Information("Added offline account: {Username}", username);
        return account;
    }

    public async Task SetActiveAccountAsync(string id)
    {
        var accounts = await LoadAccountsAsync();
        foreach (var a in accounts)
            a.IsActive = a.Id == id;
        await SaveAccountsAsync(accounts);
    }

    public async Task RemoveAccountAsync(string id)
    {
        var accounts = await LoadAccountsAsync();
        accounts.RemoveAll(a => a.Id == id);
        await SaveAccountsAsync(accounts);
    }
}
