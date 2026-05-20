using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;

namespace ObsidianLauncher.Views;

public partial class BackupManagerWindow : Window
{
    private readonly BackupManager _backupManager = null!;
    private readonly InstanceManager _instanceManager = null!;
    private Instance? _selectedInstance;
    private BackupDisplayItem? _selectedBackup;

    public BackupManagerWindow() { InitializeComponent(); }

    public BackupManagerWindow(IReadOnlyList<Instance> instances, BackupManager backupManager, InstanceManager instanceManager)
    {
        InitializeComponent();
        _backupManager = backupManager;
        _instanceManager = instanceManager;

        InstanceListBox.ItemsSource = instances;
        if (instances.Count > 0) InstanceListBox.SelectedIndex = 0;

        CreateBackupBtn.Click += CreateBackupBtn_Click;
        RestoreBtn.Click += RestoreBtn_Click;
        DeleteBtn.Click += DeleteBtn_Click;
    }

    private void InstanceListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedInstance = InstanceListBox.SelectedItem as Instance;
        _selectedBackup = null;
        RefreshBackupList();
    }

    private void BackupListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedBackup = BackupListBox.SelectedItem as BackupDisplayItem;
        RestoreBtn.IsEnabled = _selectedBackup != null;
        DeleteBtn.IsEnabled = _selectedBackup != null;
    }

    private void RefreshBackupList()
    {
        if (_selectedInstance == null)
        {
            BackupListBox.ItemsSource = null;
            NoBackupsText.IsVisible = false;
            return;
        }

        var backups = _backupManager.ListBackups(_selectedInstance)
            .Select(b => new BackupDisplayItem(b))
            .ToList();

        BackupListBox.ItemsSource = backups;
        NoBackupsText.IsVisible = backups.Count == 0;
        RestoreBtn.IsEnabled = false;
        DeleteBtn.IsEnabled = false;
    }

    private async void CreateBackupBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedInstance == null)
        {
            SetStatus("Select an instance first.");
            return;
        }

        SetStatus($"Creating backup for '{_selectedInstance.Name}'…");
        CreateBackupBtn.IsEnabled = false;
        try
        {
            var backup = await _backupManager.CreateBackupAsync(_selectedInstance, CancellationToken.None);
            if (backup != null)
                SetStatus($"Backup created: {backup.Name}");
            else
                SetStatus("Backup failed — check the log.");
        }
        finally
        {
            CreateBackupBtn.IsEnabled = true;
            RefreshBackupList();
        }
    }

    private async void RestoreBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedInstance == null || _selectedBackup == null) return;

        SetStatus($"Restoring '{_selectedBackup.Backup.Name}'…");
        RestoreBtn.IsEnabled = false;
        DeleteBtn.IsEnabled = false;
        try
        {
            var ok = await _backupManager.RestoreBackupAsync(_selectedInstance, _selectedBackup.Backup, CancellationToken.None);
            SetStatus(ok ? "Restore complete." : "Restore failed — check the log.");
        }
        finally
        {
            RestoreBtn.IsEnabled = _selectedBackup != null;
            DeleteBtn.IsEnabled = _selectedBackup != null;
        }
    }

    private void DeleteBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedBackup == null) return;
        var ok = _backupManager.DeleteBackup(_selectedBackup.Backup);
        SetStatus(ok ? $"Deleted {_selectedBackup.Backup.Name}." : "Delete failed.");
        _selectedBackup = null;
        RefreshBackupList();
    }

    private void CloseBtn_Click(object? sender, RoutedEventArgs e) => Close();

    private void SetStatus(string text) => StatusText.Text = text;
}

internal class BackupDisplayItem
{
    public BackupInfo Backup { get; }
    public string Name => Backup.Name;
    public string DateDisplay => Backup.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string SizeDisplay => Backup.SizeBytes < 1_048_576
        ? $"{Backup.SizeBytes / 1024.0:F1} KB"
        : $"{Backup.SizeBytes / 1_048_576.0:F1} MB";

    public BackupDisplayItem(BackupInfo backup) => Backup = backup;
}
