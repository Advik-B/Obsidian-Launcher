using System;

namespace ObsidianLauncher.Models;

public enum AccountType
{
    Offline,
    Microsoft
}

public class AccountInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = "";
    public AccountType Type { get; set; } = AccountType.Offline;
    public bool IsActive { get; set; }

    public string TypeDisplay => Type switch
    {
        AccountType.Offline => "Offline",
        AccountType.Microsoft => "Microsoft",
        _ => "Unknown"
    };
}
