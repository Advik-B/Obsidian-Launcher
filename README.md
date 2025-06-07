# Planned Project Structure

```
ObsidianLauncher/
├── ObsidianLauncher.sln
└── ObsidianLauncher/                     // Default Namespace: ObsidianLauncher
├── ObsidianLauncher.csproj
├── Program.cs                        // namespace ObsidianLauncher
│
├── LauncherConfig.cs                 // namespace ObsidianLauncher
│
├── Models/
│   ├── AssetIndex.cs                 // namespace ObsidianLauncher.Models
│   ├── JavaVersionInfo.cs           // namespace ObsidianLauncher.Models
│   ├── Library.cs                    // namespace ObsidianLauncher.Models
│   ├── LibraryArtifact.cs
│   ├── LibraryDownloads.cs
│   ├── LibraryExtractRule.cs
│   ├── MinecraftVersion.cs           // namespace ObsidianLauncher.Models
│   ├── DownloadDetails.cs
│   ├── OperatingSystemInfo.cs        // namespace ObsidianLauncher.Models
│   ├── Rule.cs                       // namespace ObsidianLauncher.Models
│   ├── VersionArguments.cs           // namespace ObsidianLauncher.Models
│   ├── ArgumentRuleCondition.cs
│   ├── ConditionalArgumentValue.cs
│   ├── VersionLogging.cs             // namespace ObsidianLauncher.Models
│   ├── LoggingFile.cs
│   ├── ClientLoggingInfo.cs
│   ├── VersionMetadata.cs            // namespace ObsidianLauncher.Models
│   └── JavaRuntimeInfo.cs            // namespace ObsidianLauncher.Models
│
├── Enums/
│   ├── MinecraftJarType.cs           // namespace ObsidianLauncher.Enums
│   ├── RuleAction.cs                 // namespace ObsidianLauncher.Enums
│   ├── OperatingSystemType.cs        // namespace ObsidianLauncher.Enums
│   └── ArchitectureType.cs           // namespace ObsidianLauncher.Enums
│
├── Services/
│   ├── HttpManagerService.cs         // namespace ObsidianLauncher.Services
│   ├── JavaDownloaderService.cs      // namespace ObsidianLauncher.Services
│   └── JavaManagerService.cs         // namespace ObsidianLauncher.Services
│
├── Utils/
│   ├── CryptoUtils.cs                // namespace ObsidianLauncher.Utils
│   ├── LoggerSetup.cs                // namespace ObsidianLauncher.Utils
│   └── OsUtils.cs                    // namespace ObsidianLauncher.Utils
│
└── appsettings.json
└── appsettings.Development.json
```