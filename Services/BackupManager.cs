using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Models;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

public class BackupInfo
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public long SizeBytes { get; init; }
}

public class BackupManager
{
    private readonly LauncherConfig _config;
    private readonly ILogger _logger;

    private string BackupsRoot => System.IO.Path.Combine(_config.BaseDataPath, "backups", "instances");

    public BackupManager(LauncherConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = LogHelper.GetLogger<BackupManager>();
    }

    public async Task<BackupInfo?> CreateBackupAsync(Instance instance, CancellationToken ct = default)
    {
        var backupDir = System.IO.Path.Combine(BackupsRoot, instance.Id);
        Directory.CreateDirectory(backupDir);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupFile = System.IO.Path.Combine(backupDir, $"{InstanceManager.SanitizeName(instance.Name)}_{stamp}.zip");

        try
        {
            await Task.Run(() =>
            {
                ZipFile.CreateFromDirectory(instance.InstancePath, backupFile, CompressionLevel.Optimal, false);
            }, ct);

            var info = new FileInfo(backupFile);
            _logger.Information("Created backup for '{InstanceName}' at {BackupFile}", instance.Name, backupFile);
            return new BackupInfo { Name = System.IO.Path.GetFileName(backupFile), Path = backupFile, CreatedAt = DateTime.UtcNow, SizeBytes = info.Length };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create backup for '{InstanceName}'", instance.Name);
            return null;
        }
    }

    public List<BackupInfo> ListBackups(Instance instance)
    {
        var backupDir = System.IO.Path.Combine(BackupsRoot, instance.Id);
        if (!Directory.Exists(backupDir)) return new List<BackupInfo>();

        return Directory.GetFiles(backupDir, "*.zip")
            .Select(f =>
            {
                var fi = new FileInfo(f);
                return new BackupInfo
                {
                    Name = fi.Name,
                    Path = f,
                    CreatedAt = fi.CreationTimeUtc,
                    SizeBytes = fi.Length
                };
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    public async Task<bool> RestoreBackupAsync(Instance instance, BackupInfo backup, CancellationToken ct = default)
    {
        if (!File.Exists(backup.Path))
        {
            _logger.Error("Backup file not found: {BackupPath}", backup.Path);
            return false;
        }

        try
        {
            var tempRestore = instance.InstancePath + "_restore_" + DateTime.UtcNow.Ticks;
            await Task.Run(() =>
            {
                if (Directory.Exists(instance.InstancePath))
                    Directory.Move(instance.InstancePath, tempRestore);
                ZipFile.ExtractToDirectory(backup.Path, instance.InstancePath, overwriteFiles: true);
            }, ct);

            if (Directory.Exists(tempRestore))
                Directory.Delete(tempRestore, true);

            _logger.Information("Restored instance '{InstanceName}' from backup {BackupName}", instance.Name, backup.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to restore backup for '{InstanceName}'", instance.Name);
            return false;
        }
    }

    public bool DeleteBackup(BackupInfo backup)
    {
        try
        {
            if (File.Exists(backup.Path)) File.Delete(backup.Path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete backup {BackupPath}", backup.Path);
            return false;
        }
    }
}
