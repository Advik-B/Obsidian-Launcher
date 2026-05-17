using System;
using System.Windows.Input;
using ObsidianLauncher.Models;

namespace ObsidianLauncher.ViewModels;

public class CrashReportViewModel : ViewModelBase
{
    public CrashReport Report { get; }

    public string Title => $"Crash Report — {Report.InstanceName}";
    public string ExitCodeText => $"Exit code: {Report.ExitCode}";
    public string CategoryText => Report.Category switch
    {
        CrashCategory.OutOfMemory    => "Out of Memory — try increasing maximum RAM in Instance Settings → Java",
        CrashCategory.JvmCrash       => "JVM Crash — a fatal Java error occurred (hs_err_pid file may contain details)",
        CrashCategory.ModConflict    => "Mod Conflict — two or more mods may be incompatible",
        CrashCategory.MissingDependency => "Missing Dependency — a required mod or library is absent",
        CrashCategory.GameCrash      => "Game Crash — Minecraft encountered a fatal error",
        _                            => "Unknown crash — check the log for details"
    };
    public string? Suggestion => Report.Suggestion;
    public string LogExcerpt => string.Join("\n", Report.RelevantLines);
    public bool HasSuggestion => !string.IsNullOrWhiteSpace(Report.Suggestion);

    public ICommand CloseCommand { get; }

    public CrashReportViewModel(CrashReport report)
    {
        Report = report;
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? CloseRequested;
}
