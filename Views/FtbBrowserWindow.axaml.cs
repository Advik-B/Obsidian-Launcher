using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ObsidianLauncher.Services;
using ObsidianLauncher.Services.Import;

namespace ObsidianLauncher.Views;

public partial class FtbBrowserWindow : Window
{
    private readonly FtbImporter _importer = null!;
    private CancellationTokenSource _cts = new();
    private List<FtbPackSummary> _packs = new();
    private FtbPackInfo? _selectedPackInfo;

    public FtbBrowserWindow() { InitializeComponent(); }

    public FtbBrowserWindow(InstanceManager instanceManager, HttpManager httpManager)
    {
        InitializeComponent();
        _importer = new FtbImporter(instanceManager, httpManager);

        SearchBtn.Click += SearchBtn_Click;
        SearchBox.KeyDown += SearchBox_KeyDown;
        ImportBtn.Click += ImportBtn_Click;
        VersionCombo.SelectionChanged += VersionCombo_SelectionChanged;

        // Load initial popular packs on open
        _ = LoadPacksAsync("");
    }

    private void SearchBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Return) _ = LoadPacksAsync(SearchBox.Text ?? "");
    }

    private void SearchBtn_Click(object? sender, RoutedEventArgs e) => _ = LoadPacksAsync(SearchBox.Text ?? "");

    private async Task LoadPacksAsync(string query)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetBusy(true, "Searching…");
        ImportBtn.IsEnabled = false;
        VersionCombo.IsVisible = false;
        NoVersionsText.IsVisible = true;
        PackDescText.IsVisible = false;
        _selectedPackInfo = null;

        try
        {
            _packs = await _importer.SearchAsync(query, ct);
            if (ct.IsCancellationRequested) return;

            PackListBox.ItemsSource = _packs;
            SetBusy(false, $"{_packs.Count} pack{(_packs.Count == 1 ? "" : "s")} found");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetBusy(false, $"Search failed: {ex.Message}");
        }
    }

    private void PackListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = PackListBox.SelectedItem as FtbPackSummary;
        if (selected == null) return;
        _ = LoadPackVersionsAsync(selected.Id);
    }

    private async Task LoadPackVersionsAsync(long packId)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetBusy(true, "Loading versions…");
        VersionCombo.IsVisible = false;
        NoVersionsText.IsVisible = false;
        ImportBtn.IsEnabled = false;
        PackDescText.IsVisible = false;

        try
        {
            _selectedPackInfo = await _importer.GetPackInfoAsync(packId, ct);
            if (ct.IsCancellationRequested || _selectedPackInfo == null) return;

            var versions = _selectedPackInfo.Versions ?? new();
            VersionCombo.ItemsSource = versions;
            VersionCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Name");
            if (versions.Count > 0) VersionCombo.SelectedIndex = 0;

            VersionCombo.IsVisible = versions.Count > 0;
            NoVersionsText.IsVisible = versions.Count == 0;
            NoVersionsText.Text = versions.Count == 0 ? "No versions available." : "";

            if (!string.IsNullOrWhiteSpace(_selectedPackInfo.Synopsis))
            {
                PackDescText.Text = _selectedPackInfo.Synopsis;
                PackDescText.IsVisible = true;
            }

            ImportBtn.IsEnabled = versions.Count > 0;
            SetBusy(false, $"{versions.Count} version{(versions.Count == 1 ? "" : "s")}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetBusy(false, $"Failed to load versions: {ex.Message}");
        }
    }

    private void VersionCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ImportBtn.IsEnabled = VersionCombo.SelectedItem != null;
    }

    private async void ImportBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedPackInfo == null || VersionCombo.SelectedItem is not FtbPackVersion version) return;

        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetBusy(true, $"Importing '{_selectedPackInfo.Name}'…");
        ImportBtn.IsEnabled = false;
        SearchBtn.IsEnabled = false;

        try
        {
            var progress = new Progress<(string Status, double Progress)>(r =>
            {
                Dispatcher.UIThread.Post(() => StatusText.Text = r.Status);
            });

            var instance = await _importer.ImportAsync(_selectedPackInfo.Id, version.Id, progress, ct);
            if (ct.IsCancellationRequested) return;

            if (instance != null)
            {
                SetBusy(false, $"Imported '{instance.Name}' successfully.");
                await Task.Delay(800, ct);
                Close();
            }
            else
            {
                SetBusy(false, "Import failed — check the log for details.");
                ImportBtn.IsEnabled = true;
                SearchBtn.IsEnabled = true;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetBusy(false, $"Import error: {ex.Message}");
            ImportBtn.IsEnabled = true;
            SearchBtn.IsEnabled = true;
        }
    }

    private void CancelBtn_Click(object? sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts.Cancel();
        base.OnClosed(e);
    }

    private void SetBusy(bool busy, string status)
    {
        ProgressBar.IsVisible = busy;
        StatusText.Text = status;
    }
}
