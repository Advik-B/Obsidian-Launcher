# PrismLauncher vs Obsidian Launcher - Feature Comparison

This document provides a comprehensive comparison of features between PrismLauncher and Obsidian Launcher to identify gaps and opportunities for future implementation.

## ✅ Currently Implemented in Obsidian Launcher

### Core Launcher Features
- [x] Fetch and parse Minecraft version manifest from Mojang
- [x] Download and verify Minecraft client JAR files
- [x] Download and verify game assets with SHA1 verification
- [x] Download and verify libraries with SHA1 verification
- [x] Handle native libraries (extraction for LWJGL)
- [x] Java runtime discovery and management
- [x] Java runtime download from Mojang
- [x] Build JVM arguments with placeholder replacement
- [x] Build game arguments with placeholder replacement
- [x] Construct classpath for game launch
- [x] Launch Minecraft with proper working directory
- [x] Console logging with Serilog (file and console output)
- [x] Offline mode support (no authentication)
- [x] Cross-platform support (Windows, Linux, macOS via .NET)
- [x] Rule-based library evaluation (OS-specific libraries)
- [x] Progress reporting for downloads
- [x] Cancellation support (Ctrl+C handling)

## ❌ Missing Features (Available in PrismLauncher)

### 1. Authentication & Account Management
- [ ] **Microsoft Account Authentication** - OAuth2 flow for Microsoft accounts
- [ ] **Multiple Account Management** - Support for managing multiple Minecraft accounts
- [ ] **Account Switching** - Easy switching between different accounts
- [ ] **Legacy Mojang Account Support** - Support for old Mojang accounts (being phased out)
- [ ] **Account Persistence** - Save and reload account credentials securely
- [ ] **Account Profile Selection** - Support for different skins/capes per account

### 2. User Interface
- [ ] **Graphical User Interface (GUI)** - Full Qt-based GUI application
  - Currently: Console-only interface
- [ ] **Theme Support** - Custom themes and appearance customization
- [ ] **Dark Mode / Light Mode** - Built-in theme variants
- [ ] **Custom Icons** - Per-instance custom icons
- [ ] **Drag and Drop Support** - For mods, resource packs, etc.
- [ ] **Settings Dialog** - Comprehensive settings management UI
- [ ] **Instance List View** - Visual list/grid of all instances
- [ ] **Progress Bars** - Visual download/extraction progress
- [ ] **Notification System** - In-app notifications for updates, errors, etc.

### 3. Instance Management
- [ ] **Multiple Instances** - Create and manage multiple separate Minecraft installations
- [ ] **Instance Profiles** - Each instance with separate settings, mods, resource packs
- [ ] **Instance Cloning** - Duplicate existing instances
  - [ ] Hard link support
  - [ ] Symbolic link support
  - [ ] Copy-on-write (CoW) cloning
- [ ] **Instance Renaming** - Easy instance name changes
- [ ] **Instance Groups/Folders** - Organize instances into groups
- [ ] **Instance Notes** - Add custom notes/descriptions to instances
- [ ] **Instance Screenshots** - Per-instance screenshot management
- [ ] **Instance Logs** - Per-instance log viewing and management
- [ ] **Instance Export** - Export instances as modpacks or backups
  - [ ] .zip export
  - [ ] Modrinth modpack export (.mrpack)
  - [ ] CurseForge modpack export
- [ ] **Instance Import** - Import from various sources
  - [ ] From zip archives
  - [ ] From other launchers (MultiMC, ATLauncher, etc.)
  - [ ] From modpack platforms
- [ ] **Per-Instance Settings** - Java version, memory allocation, resolution, etc.
- [ ] **Instance Locking** - Prevent accidental modifications

### 4. Modloader Support
- [ ] **Fabric Loader Integration**
  - [ ] One-click Fabric installation
  - [ ] Fabric API auto-installation
  - [ ] Version selection
- [ ] **Forge Integration**
  - [ ] One-click Forge installation
  - [ ] Version selection
  - [ ] Forge installer handling
- [ ] **NeoForge Support** (Forge fork for newer versions)
  - [ ] One-click NeoForge installation
- [ ] **Quilt Loader Integration** (Fabric fork)
  - [ ] One-click Quilt installation
  - [ ] Quilt Standard Libraries / Quilted Fabric API
- [ ] **LiteLoader Support** (legacy)
- [ ] **Mixed Modloader Support** - Multiple loaders per instance where compatible

### 5. Mod Management
- [ ] **Mod Browser Integration**
  - [ ] Modrinth mod browser
  - [ ] CurseForge mod browser
  - [ ] Search and filter mods
  - [ ] Direct download from launcher
- [ ] **Mod Installation**
  - [ ] One-click mod installation
  - [ ] Drag-and-drop mod installation
  - [ ] Automatic dependency resolution
- [ ] **Mod Updates**
  - [ ] Check for mod updates
  - [ ] One-click update
  - [ ] Bulk update all mods
- [ ] **Mod Metadata Management**
  - [ ] Track mod version, name, source
  - [ ] Display mod descriptions
  - [ ] Show mod dependencies
- [ ] **Mod Compatibility Checking**
  - [ ] Check Minecraft version compatibility
  - [ ] Check modloader compatibility
  - [ ] Warn about incompatible mods
- [ ] **Mod Enable/Disable** - Toggle mods without deleting
- [ ] **Mod Configuration** - Edit mod config files from launcher

### 6. Modpack Support
- [ ] **Modpack Installation**
  - [ ] Modrinth modpacks (.mrpack)
  - [ ] CurseForge modpacks
  - [ ] FTB (Feed The Beast) modpacks
  - [ ] ATLauncher modpacks
  - [ ] Technic modpacks
- [ ] **Modpack Updates** - Check and update installed modpacks
- [ ] **Modpack Creation** - Create custom modpacks
- [ ] **Modpack Export** - Share custom modpacks
- [ ] **Modpack Metadata** - Display modpack information, changelogs

### 7. Resource Pack & Shader Management
- [ ] **Resource Pack Browser** - Browse and download resource packs
- [ ] **Resource Pack Installation** - Install from launcher or drag-and-drop
- [ ] **Resource Pack Enable/Disable** - Manage active resource packs
- [ ] **Shader Pack Support**
  - [ ] OptiFine shader installation
  - [ ] Iris shader installation (Fabric)
- [ ] **Shader Pack Browser** - Browse and download shader packs
- [ ] **Resource Pack Updates** - Check for resource pack updates
- [ ] **Preview Support** - Preview resource packs before applying

### 8. Advanced Java Management
- [ ] **Multiple Java Version Management** - Manage multiple Java installations
- [ ] **Per-Instance Java Settings** - Different Java version per instance
- [ ] **Auto Java Download** - Automatically download required Java versions
- [ ] **Java Version Detection** - Detect system-installed Java
- [ ] **Custom Java Arguments** - Per-instance JVM arguments
- [ ] **Memory Allocation Controls** - Easy min/max memory settings
- [ ] **Java Path Selection** - Manual Java executable selection

### 9. World Management
- [ ] **World List View** - View all worlds in an instance
- [ ] **World Import** - Import worlds from files or other instances
- [ ] **World Export** - Export worlds as backups
- [ ] **World Rename** - Rename worlds
- [ ] **World Deletion** - Delete worlds
- [ ] **World Copy** - Duplicate worlds
- [ ] **Global World Manager** - Manage worlds across all instances
- [ ] **World Search** - Find worlds across instances

### 10. Screenshot Management
- [ ] **Screenshot Viewer** - View screenshots from launcher
- [ ] **Screenshot Upload** - Upload to image hosting services
- [ ] **Screenshot Organization** - Organize by instance
- [ ] **Screenshot Deletion** - Delete screenshots
- [ ] **Screenshot Rename** - Rename screenshot files
- [ ] **Screenshot Copy** - Copy to clipboard or other locations
- [ ] **Global Screenshot Manager** - View screenshots from all instances

### 11. Update & Notification System
- [ ] **Launcher Update Checker** - Check for launcher updates
- [ ] **Auto-Update Launcher** - Automatically update launcher
- [ ] **Mod Update Notifications** - Notify when mods have updates
- [ ] **Modpack Update Notifications** - Notify when modpacks have updates
- [ ] **Minecraft Version Notifications** - Notify about new Minecraft releases
- [ ] **Update Frequency Settings** - Configure update check frequency

### 12. Network & Download Management
- [ ] **Parallel Downloads** - Download multiple files simultaneously
- [ ] **Download Retry Logic** - Retry failed downloads
- [ ] **Resume Support** - Resume interrupted downloads
- [ ] **Proxy Support** - HTTP/SOCKS proxy configuration
- [ ] **Mirror Selection** - Choose download mirrors for mods/assets
- [ ] **Bandwidth Limiting** - Limit download speeds
- [ ] **Offline Mode** - Full offline operation after initial setup

### 13. Logging & Diagnostics
- [ ] **In-App Log Viewer** - View game logs from launcher
- [ ] **Log Filtering** - Filter logs by level/content
- [ ] **Log Export** - Export logs for troubleshooting
- [ ] **Crash Report Analysis** - Detect and analyze crash reports
- [ ] **Performance Monitoring** - Monitor game performance
- [ ] **Error Reporting** - Report launcher errors to developers

### 14. Launch Options & Game Settings
- [ ] **Custom Resolution** - Set game window resolution
- [ ] **Fullscreen Toggle** - Launch in fullscreen mode
- [ ] **Server Auto-Join** - Auto-join a server on launch
- [ ] **Custom Game Arguments** - Per-instance game arguments
- [ ] **Pre-Launch Commands** - Run commands before game launch
- [ ] **Post-Exit Commands** - Run commands after game exits
- [ ] **Wrapper Commands** - Launch game through wrapper (e.g., gamemode, mangohud)
- [ ] **Environment Variables** - Set custom environment variables

### 15. Advanced Features
- [ ] **Component Management** - Manage instance components (Fabric, Forge, etc.)
- [ ] **Custom Patches** - Apply custom JSON patches to version files
- [ ] **Override Management** - Override specific files in instances
- [ ] **Server List Management** - Import/export server lists
- [ ] **Skin Management** - Change Minecraft skins from launcher
- [ ] **Cape Management** - Manage Minecraft capes
- [ ] **Version Isolation** - Isolate game files per version
- [ ] **Backup System** - Automated instance backups
- [ ] **Restore System** - Restore from backups

### 16. Internationalization
- [ ] **Multi-Language Support** - Interface in multiple languages
- [ ] **Translation via Weblate** - Community-driven translations
- [ ] **Language Selection** - User-selectable interface language

### 17. Platform-Specific Features
- [ ] **Linux Integration**
  - [ ] Native package formats (.deb, .rpm, AppImage, Flatpak)
  - [ ] Desktop file integration
  - [ ] System tray integration
- [ ] **macOS Integration**
  - [ ] .app bundle
  - [ ] Dock integration
- [ ] **Windows Integration**
  - [ ] Start menu integration
  - [ ] File association (.mrpack, etc.)

### 18. Community & Sharing
- [ ] **Modpack Sharing** - Easy sharing of custom modpacks
- [ ] **Configuration Sharing** - Share launcher configurations
- [ ] **Social Features** - Discord Rich Presence integration
- [ ] **Community Forums Integration** - Access community resources

### 19. Developer Features
- [ ] **API Access** - Launcher API for extensions
- [ ] **Plugin System** - Support for launcher plugins
- [ ] **Developer Mode** - Advanced debugging features
- [ ] **Custom Meta Servers** - Use custom metadata servers
- [ ] **Build from Source** - Easy source building for customization

### 20. Security & Privacy
- [ ] **Secure Credential Storage** - Encrypted storage of account tokens
- [ ] **Two-Factor Authentication** - 2FA support for accounts
- [ ] **Privacy Settings** - Control telemetry and analytics
- [ ] **Sandboxing** - Instance isolation for security
- [ ] **Signature Verification** - Verify mod/modpack signatures

## 📊 Feature Gap Summary

### High Priority (Core Functionality)
1. **GUI Application** - Most critical missing feature
2. **Microsoft Account Authentication** - Required for modern Minecraft
3. **Instance Management** - Core feature for power users
4. **Mod Management** - Essential for modded gameplay
5. **Modloader Integration** - Fabric, Forge, Quilt support

### Medium Priority (Enhanced Experience)
6. **Modpack Support** - Popular use case
7. **Resource Pack/Shader Management** - Quality of life
8. **Update System** - Keep everything current
9. **World Management** - Important for players
10. **Advanced Java Management** - Per-instance flexibility

### Lower Priority (Nice to Have)
11. **Screenshot Management** - Convenience feature
12. **Internationalization** - Broader audience
13. **Social Features** - Community engagement
14. **Developer Features** - Extensibility
15. **Platform-Specific Integrations** - Polish

## 🎯 Recommended Implementation Order

### Phase 1: Foundation (Essential)
1. Microsoft Account Authentication
2. Multiple Account Management
3. Basic GUI Framework (Qt or Avalonia)
4. Instance Management System
5. Instance Settings (Java, Memory, etc.)

### Phase 2: Modding Support (Core Value)
1. Fabric Loader Integration
2. Forge Integration
3. Quilt Loader Integration
4. Mod Browser (Modrinth + CurseForge)
5. Mod Installation & Updates
6. Basic Modpack Support

### Phase 3: User Experience
1. Resource Pack Management
2. Shader Pack Support
3. GUI Improvements (themes, icons)
4. Update Checker
5. Screenshot Management
6. World Management

### Phase 4: Advanced Features
1. Advanced Modpack Support (all formats)
2. Instance Import/Export
3. Backup & Restore
4. Custom Launch Options
5. Log Viewer
6. Performance Monitoring

### Phase 5: Polish & Extras
1. Internationalization
2. Platform-Specific Features
3. Social Features
4. Developer Features
5. Additional Community Tools

---

## 📝 Notes

- **Current Strength**: Obsidian Launcher has a solid foundation with excellent core functionality for downloading and launching vanilla Minecraft
- **Biggest Gap**: Lack of GUI and authentication severely limits usability for end users
- **Architecture**: The codebase is well-structured and ready for expansion with services pattern
- **Technology Stack**: .NET 10 provides excellent cross-platform support and modern language features

---

*This comparison was created on 2026-01-28 based on PrismLauncher documentation and feature set.*
