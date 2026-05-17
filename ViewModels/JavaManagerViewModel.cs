using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ObsidianLauncher.Models;
using ObsidianLauncher.Services;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.ViewModels;

public class JavaManagerViewModel : ViewModelBase
{
    private readonly JavaManager _javaManager;
    private readonly ILogger _logger;

    private JavaRuntimeInfo? _selectedRuntime;
    private bool _isLoading;
    private string _statusText = "";

    public ObservableCollection<JavaRuntimeInfo> Runtimes { get; } = new();

    public JavaRuntimeInfo? SelectedRuntime
    {
        get => _selectedRuntime;
        set
        {
            if (SetProperty(ref _selectedRuntime, value))
                ((RelayCommand)RemoveCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand BrowseAddCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand CloseCommand { get; }

    public event EventHandler? CloseRequested;

    public JavaManagerViewModel(JavaManager javaManager)
    {
        _javaManager = javaManager ?? throw new ArgumentNullException(nameof(javaManager));
        _logger = LogHelper.GetLogger<JavaManagerViewModel>();

        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        BrowseAddCommand = new RelayCommand(async () => await BrowseAddAsync());
        RemoveCommand = new RelayCommand(RemoveSelected, () => SelectedRuntime != null);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusText = "Detecting Java runtimes...";
        try
        {
            await Task.Run(() =>
            {
                var runtimes = _javaManager.GetAvailableRuntimes();
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Runtimes.Clear();
                    foreach (var r in runtimes)
                        Runtimes.Add(r);
                });
            });
            StatusText = Runtimes.Count == 0 ? "No runtimes detected" : $"{Runtimes.Count} runtime(s) found";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load Java runtimes");
            StatusText = "Error loading runtimes";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task BrowseAddAsync()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
            var files = await desktop.MainWindow!.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Select Java Executable",
                    AllowMultiple = false
                });

            if (files.Count == 0) return;
            var javaPath = files[0].Path.LocalPath;
            if (!File.Exists(javaPath)) return;

            var version = await DetectVersionAsync(javaPath);
            var homePath = Path.GetDirectoryName(Path.GetDirectoryName(javaPath)) ?? Path.GetDirectoryName(javaPath) ?? javaPath;

            var runtime = new JavaRuntimeInfo
            {
                JavaExecutablePath = javaPath,
                HomePath = homePath,
                MajorVersion = version,
                ComponentName = $"java-runtime-{version}",
                Source = "user_provided"
            };

            Runtimes.Add(runtime);
            StatusText = $"Added Java {version} from {javaPath}";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add Java runtime");
            StatusText = "Failed to add runtime";
        }
    }

    private void RemoveSelected()
    {
        if (SelectedRuntime == null) return;
        Runtimes.Remove(SelectedRuntime);
        SelectedRuntime = null;
        StatusText = "Runtime removed";
    }

    private static async Task<uint> DetectVersionAsync(string javaPath)
    {
        try
        {
            using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (proc == null) return 0;
            var output = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var match = System.Text.RegularExpressions.Regex.Match(output, @"version ""(?:1\.)?(\d+)");
            if (match.Success && uint.TryParse(match.Groups[1].Value, out var major))
                return major;
        }
        catch { }
        return 0;
    }
}
