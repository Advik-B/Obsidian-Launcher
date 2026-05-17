# Plan: Fix Obsidian Launcher

## What's actually broken

### Real bugs
1. **`CreateNoWindow` Windows-only check** (`GameLauncher.cs:148`) — Uses `javaw.exe` filename comparison to decide whether to hide the console window. On Linux/macOS this is always false; the launcher will always create a visible window on those platforms. Fix: replace with `OsUtils.GetCurrentOS() == OperatingSystemType.Windows`.

2. **TAR.GZ extraction commented out** (`JavaManager.cs:348-359`) — Java on Linux/macOS is distributed as `.tar.gz`. The code has the implementation ready in comments but it's disabled, leaving Linux/macOS completely broken for Java downloads. Fix: uncomment and enable using `System.Formats.Tar` (available since .NET 7; we're on .NET 10).

3. **`LibraryManager` creates a second `AssetManager`** (`LibraryManager.cs:27`) — `new AssetManager(config, httpManager)` is called inside the constructor, spinning up a duplicate `HttpManager`-sharing instance instead of reusing the one already created in `ObsidianLauncher.cs`. Fix: inject the existing `AssetManager` as a constructor parameter and update the call site.

### Dead code (two full sets of duplicated methods)
4. **`ArgumentBuilder.cs` — duplicate `MinecraftVersion` overloads** (lines 257–424) — `BuildJvmArguments(MinecraftVersion)`, `BuildGameArguments(MinecraftVersion)`, and `ReplacePlaceholders(MinecraftVersion)` are never called anywhere. The main entry point exclusively uses the `LaunchProfile` overloads. Legacy `minecraftArguments` strings are already converted to `VersionArgument` objects inside `LaunchProfile.MergeFrom()`, so the `LaunchProfile` versions handle all cases. Delete the three `MinecraftVersion` overloads (~170 lines).

5. **`JavaManager.cs` — duplicate `EnsureJavaForMinecraftVersionAsync(MinecraftVersion)`** (lines 182–311) — Never called from `ObsidianLauncher.cs`; the `LaunchProfile` overload is the one used. Delete the `MinecraftVersion` overload (~130 lines). Add a Mojang-fallback to the remaining overload by adding a `DownloadJavaForMinecraftVersionMojangAsync` overload in `JavaDownloader` that accepts `JavaVersionInfo` directly (since `mcVersion.JavaVersion` is all it needs).

6. **`StringBuilderExtensions` dead class** (`GameLauncher.cs:249-285`) — Defined at the bottom of `GameLauncher.cs` but never called. The actual argument assembly uses a local `appendQuotedArgument` lambda. Delete the class.

### Code quality
7. **Concurrent asset download throttle** (`AssetManager.cs:112-154`) — Uses `downloadTasks.Count(t => !t.IsCompleted)` in a spin loop (O(N) LINQ per iteration). Replace with `SemaphoreSlim` for clean, allocation-free throttling.

8. **`SetCustomResolution` has no input validation** (`ArgumentBuilder.cs:209`) — Accepts `0` or negative values silently. Add a guard: if `width <= 0 || height <= 0` log a warning and reject.

9. **Stale comment clutter** — Remove leftover `// For Process and ProcessStartInfo` / `// For StringBuilder` duplicates at the tops of `GameLauncher.cs` and `JavaDownloader.cs`.

---

## What I will NOT do
- Add interfaces (`IHttpManager`, etc.) or a DI container — that's a separate architectural decision.
- Add tests — not requested.
- Implement mod loader support (Forge/Quilt/NeoForge) — new feature, out of scope.
- Touch `InstanceManager`, `ModLoaderService`, or `LauncherConfig` — they work and are not in scope.

---

## File-by-file changes

| File | Change |
|------|--------|
| `Services/ArgumentBuilder.cs` | Delete lines 257–424 (3 `MinecraftVersion` overloads) |
| `Services/JavaManager.cs` | Delete lines 182–311 (`MinecraftVersion` overload); add TAR.GZ extraction; fix `CreateNoWindow` is in GameLauncher |
| `Services/JavaManager.cs` | Uncomment + implement TAR.GZ block using `System.Formats.Tar` |
| `Services/JavaDownloader.cs` | Add `DownloadJavaForMinecraftVersionMojangAsync(JavaVersionInfo, ...)` overload; wire Mojang fallback in `JavaManager` |
| `Services/GameLauncher.cs` | Fix `CreateNoWindow` to use OS check; delete `StringBuilderExtensions`; remove stale comments |
| `Services/LibraryManager.cs` | Accept `AssetManager` as constructor parameter instead of self-instantiating |
| `Services/AssetManager.cs` | Replace throttle loop with `SemaphoreSlim` |
| `Services/ArgumentBuilder.cs` | Add validation to `SetCustomResolution` |
| `ObsidianLauncher.cs` | Update `new LibraryManager(...)` call to pass existing `assetManager` |
