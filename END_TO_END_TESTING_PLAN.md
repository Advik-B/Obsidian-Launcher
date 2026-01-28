# End-to-End Testing Plan for Obsidian Launcher

## Overview
This document provides a comprehensive testing plan for Obsidian Launcher. The application is built for Windows (win-x64) and requires a Windows environment for execution.

## Prerequisites
- Windows operating system (x64)
- .NET 10.0 Runtime installed
- Internet connection for downloading Minecraft assets
- At least 5GB of free disk space

## Test Environment Setup

### Building the Application
```bash
cd "Obsidian-Launcher"
dotnet restore "Obsidian Launcher.csproj"
dotnet build "Obsidian Launcher.csproj" -c Release
```

### Locating the Executable
```
bin/Release/net10.0/win-x64/Obsidian Launcher.exe
```

## Test Scenarios

### Test 1: Basic Application Launch (Offline Mode)
**Objective**: Verify the launcher starts and initializes properly

**Steps**:
1. Run `Obsidian Launcher.exe` without arguments
2. Check console output for initialization messages
3. Verify log file creation in `.ObsidianLauncher/logs/`

**Expected Results**:
- Application starts without errors
- Configuration directories are created:
  - `.ObsidianLauncher/`
  - `.ObsidianLauncher/java_runtimes/`
  - `.ObsidianLauncher/assets/`
  - `.ObsidianLauncher/libraries/`
  - `.ObsidianLauncher/versions/`
  - `.ObsidianLauncher/logs/`
- Serilog logging initializes successfully
- Default version (1.20.4) is targeted

**Pass Criteria**: ✅ All directories created, logs show initialization

---

### Test 2: Version Manifest Fetching
**Objective**: Verify successful fetching and parsing of Minecraft version manifest

**Steps**:
1. Run the launcher
2. Monitor console output for manifest fetch
3. Check log file for manifest details

**Expected Results**:
- HTTP request to `https://launchermeta.mojang.com/mc/game/version_manifest_v2.json` succeeds
- Manifest JSON is downloaded (should be ~500KB+)
- JSON is parsed successfully
- Log shows count of versions found (should be 900+)

**Pass Criteria**: ✅ Manifest fetched, parsed, version count logged

**Troubleshooting**:
- If fetch fails, check internet connection
- If parsing fails, check for JSON format changes by Mojang

---

### Test 3: Version-Specific Details Fetching
**Objective**: Verify downloading and parsing of specific version JSON

**Steps**:
1. Run launcher with default version (1.20.4)
2. Monitor for version details fetch
3. Verify version-specific JSON parsing

**Expected Results**:
- URL for version 1.20.4 is found in manifest
- Version JSON is downloaded (~10-20KB)
- JSON is parsed into MinecraftVersion object
- Properties are populated:
  - `Id`: "1.20.4"
  - `Type`: "release"
  - `MainClass`: (e.g., "net.minecraft.client.main.Main")
  - `JavaVersion`: (e.g., major version 17)
  - `AssetIndex`: Object with Id, Sha1, URL
  - `Libraries`: List of libraries
  - `Arguments`: JVM and game arguments

**Pass Criteria**: ✅ Version details fetched and fully parsed

---

### Test 4: Java Runtime Discovery
**Objective**: Test Java runtime discovery on the system

**Steps**:
1. Ensure Java 17+ is installed on system
2. Run the launcher
3. Check logs for Java discovery

**Expected Results**:
- JavaManager scans for existing Java installations
- If Java is found: Log shows discovered Java version and path
- If Java not found: Should proceed to download

**Pass Criteria**: ✅ Java discovery works (finds or downloads Java)

**Troubleshooting**:
- If Java not found, ensure JAVA_HOME is set or Java is in PATH
- If download fails, check internet connection

---

### Test 5: Java Runtime Download (if needed)
**Objective**: Test automatic Java runtime download

**Steps**:
1. Ensure no compatible Java runtime exists in `.ObsidianLauncher/java_runtimes/`
2. Run the launcher
3. Monitor download progress

**Expected Results**:
- JavaManager identifies required Java version (e.g., java-runtime-gamma, major version 17)
- Fetches Mojang's Java runtime manifest
- Downloads Java runtime from Mojang CDN
- Extracts runtime to `.ObsidianLauncher/java_runtimes/`
- Verifies Java executable existence
- Logs Java executable path

**Pass Criteria**: ✅ Java downloaded, extracted, and verified

**File Size**: Expect ~50-100MB download

---

### Test 6: Asset Index Download
**Objective**: Verify asset index download and verification

**Steps**:
1. Clear `.ObsidianLauncher/assets/indexes/` if testing fresh
2. Run the launcher
3. Monitor asset index download

**Expected Results**:
- Asset index ID is extracted from version JSON (e.g., "12" for 1.20.4)
- Asset index JSON is downloaded
- SHA1 hash is verified
- Index is saved to `.ObsidianLauncher/assets/indexes/12.json`
- Log shows asset index details

**Pass Criteria**: ✅ Asset index downloaded and verified

---

### Test 7: Asset Objects Download
**Objective**: Verify individual asset objects download

**Steps**:
1. Clear `.ObsidianLauncher/assets/objects/` if testing fresh
2. Run the launcher
3. Monitor asset download progress

**Expected Results**:
- Asset index JSON is parsed
- Each asset object is processed:
  - Hash is extracted
  - Target path is calculated (e.g., `objects/de/deadbeef...`)
  - File is downloaded from `https://resources.download.minecraft.net/`
  - SHA1 hash is verified
- Progress is reported (e.g., "1234/5678 files (55%)")
- All assets are successfully downloaded

**Pass Criteria**: ✅ All assets downloaded with hash verification

**File Size**: Expect 200-500MB total for assets
**Time**: May take 5-15 minutes depending on connection

**Troubleshooting**:
- If downloads fail, check internet connection
- If hash verification fails, re-download the affected file

---

### Test 8: Library Download and Verification
**Objective**: Test library download and SHA1 verification

**Steps**:
1. Clear `.ObsidianLauncher/libraries/` if testing fresh
2. Run the launcher
3. Monitor library processing

**Expected Results**:
- Each library in version JSON is processed
- Rules are evaluated (OS-specific libraries)
- Applicable libraries are downloaded:
  - Main artifact JAR
  - Native libraries (if applicable for Windows)
- SHA1 hashes are verified
- Libraries are saved to correct paths
- Progress is reported
- Classpath entries are tracked

**Pass Criteria**: ✅ All applicable libraries downloaded and verified

**File Size**: Expect 50-150MB total
**Count**: ~50-80 libraries for typical version

---

### Test 9: Native Library Extraction
**Objective**: Verify native library extraction for LWJGL

**Steps**:
1. Run the launcher
2. Monitor native extraction process

**Expected Results**:
- Native libraries are identified (e.g., lwjgl platform-specific JARs)
- Natives directory is created (e.g., `.ObsidianLauncher/versions/1.20.4/1.20.4-natives/`)
- Native JARs are extracted
- Exclusion rules are applied (e.g., META-INF/** excluded)
- Extracted files include platform-specific DLLs for Windows
- Log shows extraction completion

**Pass Criteria**: ✅ Natives extracted to correct directory

**Expected Files**: ~10-20 DLL files for Windows

---

### Test 10: Client JAR Download
**Objective**: Verify Minecraft client JAR download

**Steps**:
1. Clear version-specific directory if testing fresh
2. Run the launcher
3. Monitor client JAR download

**Expected Results**:
- Client download URL is extracted from version JSON
- JAR is downloaded to `.ObsidianLauncher/versions/1.20.4/1.20.4.jar`
- SHA1 hash is verified
- File size matches expected size from JSON
- Log confirms client JAR readiness

**Pass Criteria**: ✅ Client JAR downloaded and verified

**File Size**: Expect 20-30MB for typical version

---

### Test 11: Classpath Construction
**Objective**: Test classpath building for Java

**Steps**:
1. Run the launcher
2. Check logs for classpath construction

**Expected Results**:
- Classpath includes client JAR
- Classpath includes all library JARs
- Path separator is correct for Windows (semicolon `;`)
- Paths are absolute
- Log shows classpath entry count
- Log may show sample classpath entries

**Pass Criteria**: ✅ Classpath constructed with all required JARs

---

### Test 12: JVM Arguments Construction
**Objective**: Verify JVM argument building with placeholder replacement

**Steps**:
1. Run the launcher
2. Review JVM arguments in logs

**Expected Results**:
- JVM arguments from version JSON are processed
- Rules are evaluated (OS-specific, feature-specific)
- Placeholders are replaced:
  - `${natives_directory}`: Path to natives
  - `${launcher_name}`: "ObsidianLauncher.NET"
  - `${launcher_version}`: "0.1"
  - `${classpath}`: Constructed classpath
  - `${version_name}`: "1.20.4"
  - `${game_directory}`: Base data path
  - `${assets_root}`: Assets directory
  - `${assets_index_name}`: Asset index ID
- Memory arguments are included (e.g., `-Xmx2G`, `-Xms2G`)
- Logging configuration argument is added (if applicable)
- All paths are properly quoted if they contain spaces

**Pass Criteria**: ✅ JVM arguments correctly built with all placeholders replaced

---

### Test 13: Game Arguments Construction
**Objective**: Test game argument building

**Steps**:
1. Run the launcher
2. Review game arguments in logs

**Expected Results**:
- Game arguments from version JSON are processed
- Placeholders are replaced:
  - `${auth_player_name}`: "Player123" (or configured name)
  - `${version_name}`: "1.20.4"
  - `${game_directory}`: Base data path
  - `${assets_root}`: Assets directory
  - `${assets_index_name}`: Asset index ID
  - `${auth_uuid}`: Generated UUID
  - `${auth_access_token}`: "0" (offline)
  - `${user_type}`: "legacy" (offline)
  - `${version_type}`: "release"
- Feature-based arguments are handled correctly
- All paths are properly quoted

**Pass Criteria**: ✅ Game arguments correctly built

---

### Test 14: Minecraft Launch
**Objective**: Verify successful Minecraft game launch

**Steps**:
1. Run the launcher (ensure all previous steps have completed)
2. Monitor for game process start
3. Observe Minecraft window appearance
4. Check game functionality

**Expected Results**:
- Process starts with Java executable
- JVM arguments are passed correctly
- Main class is invoked
- Game arguments are passed correctly
- Working directory is set correctly
- Minecraft window appears
- Minecraft main menu loads
- No immediate crashes
- Game logs are captured by launcher
- Exit code is logged when game closes

**Pass Criteria**: ✅ Minecraft starts and reaches main menu

**Troubleshooting**:
- If process starts but crashes immediately, check logs for errors
- If window doesn't appear, check JVM arguments
- If assets fail to load, verify asset download completion

---

### Test 15: Game Output Capture
**Objective**: Verify launcher captures game output

**Steps**:
1. Launch Minecraft
2. Check if game output appears in launcher console
3. Verify log capture

**Expected Results**:
- Standard output from Minecraft is captured
- Standard error from Minecraft is captured
- Logs are displayed in launcher console
- Logs are written to launcher log file
- Both launcher and game logs are distinguishable

**Pass Criteria**: ✅ Game output captured and logged

---

### Test 16: Graceful Shutdown
**Objective**: Test clean shutdown process

**Steps**:
1. Start the launcher
2. Press Ctrl+C before game starts
3. Verify clean shutdown

**Expected Results**:
- Cancellation is detected
- "Cancellation requested" message appears
- In-progress operations are cancelled
- Logging system is flushed and closed
- Process exits cleanly
- No orphaned processes

**Pass Criteria**: ✅ Clean cancellation and shutdown

---

### Test 17: Multiple Version Support
**Objective**: Test launching different Minecraft versions

**Steps**:
1. Run launcher with version argument: `"Obsidian Launcher.exe" 1.19.4`
2. Repeat with: `"Obsidian Launcher.exe" 1.21`
3. Test with snapshot: `"Obsidian Launcher.exe" 24w10a`

**Expected Results**:
- Each version is fetched correctly
- Version-specific assets are downloaded
- Version-specific libraries are downloaded
- Each version launches successfully
- Versions don't interfere with each other

**Pass Criteria**: ✅ Multiple versions work independently

---

### Test 18: Resumability and Caching
**Objective**: Verify that re-running doesn't re-download

**Steps**:
1. Run launcher once (full download)
2. Close after successful launch
3. Run launcher again with same version

**Expected Results**:
- Existing files are detected
- SHA1 verification passes for existing files
- No re-downloads occur (or minimal)
- Launch is much faster second time
- Game still launches successfully

**Pass Criteria**: ✅ Existing files are reused, no unnecessary downloads

---

### Test 19: Error Handling - Network Failure
**Objective**: Test behavior when network is unavailable

**Steps**:
1. Disable internet connection
2. Run launcher (with no cached files)
3. Observe error handling

**Expected Results**:
- HTTP requests fail gracefully
- Clear error messages are logged
- Launcher doesn't crash
- User-friendly error information is provided
- Process exits with non-zero code

**Pass Criteria**: ✅ Graceful failure with clear errors

---

### Test 20: Error Handling - Corrupted Files
**Objective**: Test SHA1 verification failure handling

**Steps**:
1. Download files normally
2. Manually corrupt a library JAR or asset file
3. Re-run launcher

**Expected Results**:
- SHA1 verification detects corruption
- Corrupted file is re-downloaded
- Log shows which file was corrupted
- Game launches successfully after re-download

**Pass Criteria**: ✅ Corruption detected and fixed

---

### Test 21: Disk Space Handling
**Objective**: Test behavior when disk is full

**Steps**:
1. Fill disk to near capacity (testing environment)
2. Run launcher

**Expected Results**:
- Download failures are handled
- Clear error messages about disk space
- Launcher doesn't leave partial files
- Process exits gracefully

**Pass Criteria**: ✅ Proper error for disk space issues

---

### Test 22: Log File Verification
**Objective**: Verify logging system functionality

**Steps**:
1. Run launcher
2. Review log files in `.ObsidianLauncher/logs/`

**Expected Results**:
- Log files are created with timestamps
- Log levels are appropriate (Info, Warning, Error, etc.)
- Logs are detailed and useful for debugging
- No sensitive information in logs
- Log rotation works (if implemented)
- Both console and file sinks work

**Pass Criteria**: ✅ Comprehensive, useful logs

---

## Performance Benchmarks

### First-Time Setup (Cold Start)
- **Time**: 10-30 minutes (depends on internet speed)
- **Downloads**: ~500MB - 1GB total
- **Files Created**: 5000+ files

### Subsequent Launches (Warm Start)
- **Time**: 5-15 seconds
- **Downloads**: Minimal (only updates)
- **Files Checked**: All existing files verified

### Resource Usage
- **Memory**: ~100-200MB for launcher
- **Disk Space**: ~1-2GB per version
- **Network**: Burst during download, minimal after

## Known Limitations (For Testing Awareness)

1. **Windows-Only Executable**: Built for win-x64, won't run on Linux/macOS without platform-specific build
2. **Offline Mode Only**: No Microsoft authentication yet
3. **Console-Only Interface**: No GUI
4. **Single Instance**: Doesn't support multiple Minecraft instances
5. **No Mod Support**: Vanilla Minecraft only
6. **No Update Checker**: No launcher self-update

## Regression Testing Checklist

After any code changes, re-test these critical paths:

- [ ] Application initialization
- [ ] Version manifest fetch
- [ ] Java runtime management
- [ ] Asset download and verification
- [ ] Library download and verification
- [ ] Game launch
- [ ] Error handling
- [ ] Logging functionality

## Test Result Template

```
Test Date: YYYY-MM-DD
Tester: [Name]
Environment: Windows [Version], .NET 10.0
Version Tested: Obsidian Launcher [Version]

Test Results:
✅ Test 1: Basic Application Launch - PASSED
✅ Test 2: Version Manifest Fetching - PASSED
...

Issues Found:
1. [Description]
2. [Description]

Overall Status: PASS / FAIL / PARTIAL
Notes: [Any additional observations]
```

---

## Automated Testing Recommendations

For future development, consider:

1. **Unit Tests**: Test individual services (HttpManager, ArgumentBuilder, etc.)
2. **Integration Tests**: Test service interactions
3. **Mock HTTP Responses**: Test without internet dependency
4. **CI/CD Pipeline**: Automated build and test on commits
5. **Performance Tests**: Track download speeds, launch times
6. **Compatibility Matrix**: Test across Windows versions

---

*This testing plan covers .NET 10 upgrade and comprehensive end-to-end testing scenarios for Obsidian Launcher.*
