# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build "Obsidian Launcher.sln"

# Run
dotnet run --project "Obsidian Launcher.csproj"

# Run tests
dotnet test Tests/ObsidianLauncher.Tests.csproj

# Run a single test class
dotnet test Tests/ObsidianLauncher.Tests.csproj --filter "FullyQualifiedName~PrismParityTests"

# Run a single test method
dotnet test Tests/ObsidianLauncher.Tests.csproj --filter "FullyQualifiedName~PrismParityTests.SomeTestMethod"

# Publish self-contained for a platform
dotnet publish "Obsidian Launcher.csproj" -c Release -r linux-x64 --self-contained
```

Tests live in `Tests/` as a separate project (`ObsidianLauncher.Tests.csproj`) that is **not** compiled as part of the main csproj (excluded via `<Compile Remove="Tests\**" />`).

## Architecture

### Pattern: MVVM with manual wiring

The UI is **Avalonia 11** + **FluentAvalonia** (not WPF). The project uses MVVM but without a framework — `ViewModelBase` provides `INotifyPropertyChanged`, and `RelayCommand` (defined at the bottom of `MainWindowViewModel.cs`) wraps `Func<Task>` or `Action`. All service instances are created manually in `MainWindowViewModel`'s constructor (no DI container).

```
App.axaml / App.axaml.cs   ← entry; loads FluentTheme + FluentAvaloniaTheme
Views/*.axaml               ← Avalonia XAML UI
Views/*.axaml.cs            ← code-behind (minimal; event handlers only)
ViewModels/*ViewModel.cs    ← business logic + commands + state
Services/                   ← all backend logic (no UI dependencies)
Models/                     ← plain data classes
Settings/                   ← typed settings backed by TOML
Utils/                      ← cross-cutting helpers
LauncherConfig.cs           ← singleton path registry; creates all data dirs on construction
```

### Service dependency graph

`MainWindowViewModel` owns all services and wires them in its constructor:

```
HttpManager
  └── JavaManager (+ HttpManager)
  └── AssetManager (+ LauncherConfig + HttpManager)
       └── LibraryManager (+ LauncherConfig + HttpManager + AssetManager)
            └── InstanceManager (+ LauncherConfig + AssetManager + LibraryManager + HttpManager)
  └── ModLoaderService
  └── ModrinthClient → ModManager
  └── UpdateChecker
ArgumentBuilder (+ LauncherConfig)
GameLauncher (+ LauncherConfig)
BackupManager (+ LauncherConfig)
InstanceGroupManager (+ LauncherConfig)
```

### Settings system

`Settings/SettingsManager` wraps a `TomlFile` and supports hierarchical overrides (global → per-instance). Settings are registered via `RegisterString/Int/Bool/Long` and auto-save on change. `Settings/LauncherSettings` defines the concrete settings keys used by the launcher. Per-instance settings override global defaults.

### Data directories

All runtime data lives under `.ObsidianLauncher/` (relative to CWD) — defined and created by `LauncherConfig`. Key subdirs: `instances/`, `libraries/`, `assets/`, `java_runtimes/`, `versions/`, `logs/`.

### Instance model

Each instance is a directory under `instances/<name>/` containing an `instance.json`. `InstanceManager` handles creation, loading, saving, copying, deletion (with backup), and export. The `Instance` model carries both metadata (name, group, playtime) and launch config (JVM args, env vars, pre/post-launch commands, QuickPlay settings).

### Launch pipeline

`MainWindowViewModel.LaunchInstanceAsync()` orchestrates:
1. `InstanceManager.ResolveLaunchArtifactsAsync()` — builds `LaunchProfile` from stored version JSON, no network
2. `JavaManager.EnsureJavaForMinecraftVersionAsync()` — downloads Java if missing
3. `ArgumentBuilder.BuildClasspath/JvmArguments/GameArguments()` — assembles process args
4. `GameLauncher.LaunchAsync()` — spawns process, streams output to `ConsoleWindow`
5. Crash analysis via `AnalyzeCrash()` if exit code ≠ 0

### Known issues (from `.claude/plan.md`)

- `CreateNoWindow` check in `GameLauncher.cs` uses `javaw.exe` string comparison instead of OS check
- TAR.GZ extraction for Java on Linux/macOS is commented out in `JavaManager.cs`
- `LibraryManager` currently creates a second `AssetManager` internally instead of accepting one via constructor
- `ArgumentBuilder.cs` has dead `MinecraftVersion` overloads (lines ~257–424)
- `AssetManager.cs` uses a spin-loop for download throttling instead of `SemaphoreSlim`
