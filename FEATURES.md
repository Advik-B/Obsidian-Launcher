# PrismLauncher Feature Implementation Roadmap for Obsidian Launcher

This document provides a comprehensive checklist of features from PrismLauncher that should be implemented in Obsidian Launcher, organized by complexity level (Low → Critical).

## ⚠️ Important Notes

### Plugin System Support
**Obsidian Launcher is designed to support .NET plugins** for extensibility and customization. The plugin system will allow:
- **Mod loaders**: Custom implementations for Forge, Fabric, Quilt, etc.
- **Authentication providers**: Microsoft Account, custom auth backends
- **Theme providers**: Custom UI themes and skins
- **Instance providers**: Custom instance types and formats
- **Download providers**: Custom mod platforms beyond Modrinth/CurseForge

**Plugin Architecture Requirements:**
- Plugin discovery via assembly scanning
- Dependency injection for plugin services
- Event-based plugin lifecycle (load, enable, disable, unload)
- Sandboxed execution with configurable permissions
- Version compatibility checking
- Hot-reload support for development

**Implementation Priority:** Medium (Level 2-3)  
**Status:** Planned - Framework design in progress

### Configuration Format
**All configuration files use TOML format** (not INI) for better type safety, readability, and standard compliance.

## Legend
- ✅ Already Implemented
- 🚧 Partially Implemented
- ❌ Not Implemented
- 🎯 High Priority
- 💡 Enhancement Opportunity
- 🔌 Plugin-Extensible Feature

---

## Feature Implementation Tree

### 🟢 Level 1: LOW COMPLEXITY (Foundation & Utilities)

#### 1.1 Basic File Utilities
- ✅ File system operations (copy, move, delete)
- ✅ Directory creation and management
- ✅ Path normalization and validation
- ✅ Recursive directory watching for changes
- ✅ File size formatting (human-readable)
- ✅ Disk space checking utilities

#### 1.2 String & Data Utilities
- ✅ JSON serialization/deserialization
- ✅ SHA1 checksum validation
- ✅ SHA256 checksum validation
- ✅ Version number parsing and comparison
- ✅ String template/placeholder system (basic)
- ✅ UUID generation utilities

#### 1.3 OS & Platform Detection
- ✅ Operating system detection (Windows, Linux, macOS)
- ✅ Architecture detection (x86, x64, ARM, ARM64)
- ✅ System information gathering (RAM, CPU, GPU)
- ✅ Platform-specific path resolution
- ✅ Environment variable helpers

#### 1.4 Basic Configuration
- ✅ TOML file reading/writing
- ✅ Configuration versioning
- ✅ Default value handling
- ✅ Configuration validation

#### 1.5 Basic Logging Enhancements
- ✅ Console logging (Serilog)
- ✅ File logging (Serilog)
- ❌ Log level filtering UI
- ✅ Log file rotation
- ❌ Log export functionality

---

### 🟡 Level 2: MEDIUM COMPLEXITY (Core Features)

#### 2.1 Settings System
- ❌ Hierarchical settings (global + per-instance) 🎯 ✅ (Implemented)
- ❌ Settings persistence (TOML-based) ✅ (Implemented)
- ❌ Settings override mechanism ✅ (Implemented)
- ❌ Settings UI pages/dialogs
- ❌ Import/Export settings
- ❌ Settings validation and defaults ✅ (Implemented)

#### 2.3 Instance Management Enhancements
- ✅ Basic instance creation
- ✅ Instance directory organization
- ❌ Instance grouping/categorization 🎯
- ❌ Instance copying functionality
- ❌ Instance deletion with confirmation
- ❌ Instance metadata editing (name, notes, icon)
- ❌ Instance icon customization
- ❌ Instance import/export

#### 2.4 Java Runtime Management (Enhanced)
- ✅ Java auto-detection
- ✅ Java version checking
- ✅ Java download from Adoptium/Mojang
- ❌ Java installation management UI
- ❌ Multiple Java version support
- ❌ Java memory configuration UI
- ❌ Java argument presets

#### 2.5 Basic GUI Framework
- ❌ Main window with instance list 🎯
- ❌ Instance selection/launching UI
- ❌ Progress bars for downloads
- ❌ Status bar with system info
- ❌ About dialog
- ❌ Basic menu system
- ❌ Toolbar with common actions

#### 2.6 Theme System
- ❌ Dark theme
- ❌ Light theme
- ❌ System theme detection
- ❌ Custom theme support
- ❌ Theme preview/switching

#### 2.7 News & Updates
- ❌ News feed display
- ❌ Markdown rendering for news
- ❌ Update checking
- ❌ Release notes display
- ❌ Auto-update notifications

#### 2.8 Network Enhancements
- ✅ Basic HTTP downloads
- ✅ Checksum validation (SHA1)
- ❌ Concurrent download management
- ❌ Download queue system
- ❌ Bandwidth throttling
- ❌ Proxy support
- ❌ HTTP caching with validation
- ❌ Download resume support

#### 2.9 Asset & Library Management (Enhanced)
- ✅ Asset downloading
- ✅ Library downloading
- ✅ Native library extraction
- ❌ Parallel asset downloading
- ❌ MetaCache system for metadata
- ❌ Asset verification and repair
- ❌ Library deduplication

#### 2.10 Basic Resource Management
- ❌ Resource pack folder management
- ❌ Shader pack folder management
- ❌ World saves folder management
- ❌ Screenshots folder management
- ❌ Basic file browser for resources

---

### 🟠 Level 3: HIGH COMPLEXITY (Advanced Features)

#### 3.1 Advanced GUI Components
- ❌ Instance settings dialog (multi-page) 🎯
- ❌ Version selection dialog
- ❌ Account management dialog
- ❌ Settings dialog (global)
- ❌ Console output viewer
- ❌ Log viewer with filtering
- ❌ Screenshot viewer/browser
- ❌ Custom widgets (instance view, group view)

#### 3.2 Profile/Version Management
- 🚧 Minecraft version selection (basic)
- ❌ Snapshot version support
- ❌ Version filtering (release, snapshot, old-alpha, old-beta)
- ❌ Version component system (layered)
- ❌ Custom version creation
- ❌ Version JSON editing
- ❌ Component dependency resolution

#### 3.3 Authentication System
- ✅ Offline mode
- ❌ Microsoft Account (MSA) authentication 🎯
- ❌ Xbox Live integration
- ❌ Device code flow for MSA login
- ❌ Account list management
- ❌ Account persistence
- ❌ Token refresh mechanism
- ❌ Legacy Yggdrasil authentication
- ❌ Account switching

#### 3.4 Skin Management
- ❌ Skin upload
- ❌ Skin preview (3D model)
- ❌ Cape management
- ❌ Skin change/reset
- ❌ Skin library/browser

#### 3.5 Launch Configuration
- ✅ Basic game launch
- ✅ JVM argument building
- ✅ Game argument building
- ❌ Pre-launch commands
- ❌ Post-launch commands
- ❌ Custom environment variables
- ❌ Game window size configuration
- ❌ Fullscreen mode toggle
- ❌ Server auto-connect
- ❌ Wrapper commands (e.g., MangoHud)

#### 3.6 Launch Process Management
- ✅ Process creation and management
- ✅ Log capture and display
- ❌ Game log parsing and formatting
- ❌ Crash detection
- ❌ Crash report generation
- ❌ Game performance monitoring
- ❌ Play time tracking and statistics

#### 3.7 Advanced Instance Features
- ❌ Instance notes/description
- ❌ Instance tags/labels
- ❌ Instance search/filter
- ❌ Instance sorting options
- ❌ Instance templates
- ❌ Instance backup/restore

#### 3.8 Mod Loader Support (Enhanced)
- 🚧 Fabric support (basic)
- ❌ Forge support 🎯 🔌
- ❌ NeoForge support 🔌
- ❌ Quilt support 🔌
- ❌ LiteLoader support (legacy) 🔌
- ❌ Mod loader auto-detection 🔌
- ❌ Mod loader version selection UI

#### 3.9 Translation/Internationalization
- ❌ Multi-language support
- ❌ Translation files (JSON/Qt TS)
- ❌ Language selection UI
- ❌ RTL language support
- ❌ Dynamic language switching

---

### 🔴 Level 4: CRITICAL COMPLEXITY (Complex Systems)

#### 4.1 Mod Management System
- ❌ Local mod parsing and metadata extraction 🎯
- ❌ Mod list UI with enable/disable
- ❌ Mod dependency resolution
- ❌ Mod conflict detection
- ❌ Mod update checking
- ❌ Mod search and filtering
- ❌ Mod sorting (name, version, date)
- ❌ Mod metadata editing
- ❌ Mod installation from file
- ❌ Mod removal

#### 4.2 Mod Platform Integration
##### 4.2.1 Modrinth Integration
- ❌ Modrinth API client 🎯
- ❌ Mod search on Modrinth
- ❌ Mod download from Modrinth
- ❌ Modpack installation from Modrinth
- ❌ Modpack export to Modrinth format
- ❌ Version compatibility checking
- ❌ Update notifications for Modrinth mods

##### 4.2.2 CurseForge/Flame Integration
- ❌ CurseForge API client 🎯
- ❌ Mod search on CurseForge
- ❌ Mod download from CurseForge
- ❌ Modpack installation from CurseForge
- ❌ Modpack export to CurseForge format
- ❌ Checksum validation for CurseForge files
- ❌ Update notifications for CurseForge mods

##### 4.2.3 ATLauncher Integration
- ❌ ATLauncher pack manifest parsing
- ❌ Optional mod selection dialogs
- ❌ Share code support
- ❌ ATLauncher instance creation

##### 4.2.4 Technic Integration
- ❌ Technic Solder server support
- ❌ Technic pack installation
- ❌ Technic ZIP pack handling

##### 4.2.5 FTB Integration
- ❌ Legacy FTB pack support
- ❌ New FTB pack importer
- ❌ Private pack management

##### 4.2.6 Packwiz Support
- ❌ Packwiz format parsing
- ❌ Packwiz instance creation
- ❌ Packwiz export

#### 4.3 Resource Management (Advanced)
- ❌ Resource pack metadata parsing
- ❌ Resource pack preview
- ❌ Resource pack enable/disable
- ❌ Shader pack management
- ❌ Texture pack management (legacy)
- ❌ Data pack management
- ❌ Resource sorting and filtering
- ❌ Resource import/export

#### 4.4 World Management
- ❌ World list display
- ❌ World metadata parsing
- ❌ World preview (icon, size, last played)
- ❌ World backup/restore
- ❌ World copy/rename
- ❌ World deletion with confirmation
- ❌ World import/export

#### 4.5 Screenshot Management
- ❌ Screenshot capture integration
- ❌ Screenshot thumbnail generation
- ❌ Screenshot browser/gallery
- ❌ Screenshot upload (Imgur API)
- ❌ Screenshot deletion
- ❌ Screenshot export/share

#### 4.6 Server List Management
- ❌ Server list parsing (servers.dat)
- ❌ Server list editor UI
- ❌ Server add/edit/delete
- ❌ Server icon display
- ❌ Server status checking (RCON/Query)

#### 4.7 Launch System (Advanced)
##### 4.7.1 Launch Pipeline Steps
- ✅ Java version checking
- ✅ Game folder creation
- ✅ Native library extraction
- ❌ JAR modding/patching
- ❌ Asset reconstruction
- ❌ Mod folder scanning
- ❌ Launcher part launch
- ❌ Java installation automation
- ❌ Print instance info

##### 4.7.2 Process Management
- ✅ Process creation and monitoring
- ❌ Process priority setting
- ❌ Process affinity (CPU core selection)
- ❌ Memory limit enforcement
- ❌ Process cleanup on exit

#### 4.8 External Tool Integration
- ❌ External editor configuration
- ❌ Profiler integration (e.g., Spark)
- ❌ MCEdit integration
- ❌ NBTExplorer integration
- ❌ Custom tool launcher
- ❌ Tool command templates

#### 4.9 Update System
- ❌ Launcher self-update checking
- ❌ Launcher self-update downloading
- ❌ Launcher self-update installation
- ❌ Update release selection
- ❌ Update channels (stable, beta, dev)
- ❌ Automatic update notifications

#### 4.10 Data Migration
- ❌ Import from other launchers (MultiMC, etc.)
- ❌ Instance directory migration
- ❌ Configuration migration
- ❌ Account migration
- ❌ Settings migration

#### 4.11 Advanced Settings
- ❌ API endpoint configuration
- ❌ Proxy settings (HTTP, SOCKS)
- ❌ Memory allocation settings
- ❌ Java argument templates
- ❌ Game argument templates
- ❌ Custom launcher metadata URL
- ❌ Analytics opt-in/out

---

## Priority Implementation Order

### Phase 1: Foundation (Weeks 1-2)
1. ✅ Basic utilities (file, string, crypto)
2. Settings system (hierarchical INI-based)
3. Basic GUI framework (window, menu, toolbar)
4. Theme system (dark/light)

### Phase 2: Core Features (Weeks 3-5)
5. Instance management UI (list, create, delete, copy)
6. Instance grouping and categorization
7. Java management UI
8. Progress reporting UI
9. Console/log viewer

### Phase 3: Authentication & Launch (Weeks 6-8)
10. Microsoft Account authentication
11. Account management UI
12. Launch configuration UI
13. Advanced launch pipeline steps

### Phase 4: Mod Support (Weeks 9-12)
14. Local mod management
15. Modrinth integration
16. CurseForge integration
17. Mod update checking

### Phase 5: Advanced Features (Weeks 13-16)
18. Resource management (packs, shaders, worlds)
19. Screenshot management
20. Server list management
21. External tool integration

### Phase 6: Platform Integrations (Weeks 17-20)
22. ATLauncher support
23. Technic support
24. FTB support
25. Packwiz support

### Phase 7: Polish & Enhancement (Weeks 21-24)
26. Update system
27. Data migration tools
28. Translation system
29. Advanced settings
30. Performance optimization

---

## Implementation Notes

### Technology Stack Mapping
| PrismLauncher (C++) | Obsidian Launcher (C#) | Notes |
|---------------------|------------------------|-------|
| Qt 6 Widgets | Avalonia/WPF/WinUI | Modern .NET UI framework needed |
| Qt Network | HttpClient | ✅ Already implemented |
| Qt Concurrent | Task Parallel Library | .NET async/await |
| QuaZip | System.IO.Compression | ✅ Already implemented |
| OpenSSL | System.Security.Cryptography | .NET built-in |
| Qt Settings | INI/JSON persistence | Need to implement |

### Design Considerations
1. **UI Framework**: Choose Avalonia (cross-platform) or platform-specific (WPF/WinUI)
2. **Async Patterns**: Leverage C# async/await instead of Qt signals/slots
3. **Dependency Injection**: Use .NET DI container for service management
4. **MVVM Pattern**: Implement Model-View-ViewModel for UI separation
5. **Testing**: Add unit tests for each new feature
6. **Documentation**: Update docs as features are implemented

### Critical Dependencies
- ✅ .NET 10.0 SDK
- ✅ Serilog for logging
- ✅ System.Text.Json for serialization
- ❌ UI framework (Avalonia/WPF/WinUI)
- ❌ OAuth library for Microsoft auth
- ❌ Image processing library for screenshots/skins
- ❌ 3D rendering library for skin preview (optional)

---

## Progress Tracking

**Current Status**: Phase 1 (Foundation)
- Core utilities: ~80% complete
- Settings system: 0% complete
- GUI framework: 0% complete
- Theme system: 0% complete

**Overall Progress**: ~15% of total features implemented

**Next Steps**:
1. Choose and set up UI framework
2. Implement settings system
3. Create basic main window
4. Add instance list view
5. Implement theme support

---

## References

- [PrismLauncher GitHub](https://github.com/PrismLauncher/PrismLauncher)
- [PrismLauncher Documentation](https://prismlauncher.org/wiki/)
- [Minecraft Version Manifest API](https://minecraft.net/versions/manifest.json)
- [Modrinth API Docs](https://docs.modrinth.com/)
- [CurseForge API Docs](https://docs.curseforge.com/)
- [Microsoft Identity Platform](https://docs.microsoft.com/en-us/azure/active-directory/develop/)
- [Avalonia UI](https://avaloniaui.net/)

---

**Last Updated**: 2026-01-28
**Obsidian Launcher Version**: 0.1.0 (Development)
**Target PrismLauncher Version**: Latest (analyzed from main branch)
