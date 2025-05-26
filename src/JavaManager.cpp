// src/JavaManager.cpp
#include <Launcher/JavaManager.hpp>
#include <Launcher/Utils/OS.hpp>
#include <Launcher/Utils/Logger.hpp>
#include <miniz_cpp.hpp>
#include <fstream>
#include <algorithm>
// #include <iostream> // Removed

namespace Launcher {

JavaManager::JavaManager(const Config& config) : m_config(config), m_javaDownloader() {
    m_logger = Utils::Logger::GetOrCreateLogger("JavaManager");
    m_logger->trace("Initializing...");
    if (!std::filesystem::exists(m_config.javaRuntimesDir)) {
        m_logger->info("Java runtimes directory {} does not exist. Creating.", m_config.javaRuntimesDir.string());
        std::filesystem::create_directories(m_config.javaRuntimesDir);
    }
    scanForExistingRuntimes();
    m_logger->trace("Initialization complete. Found {} existing runtimes.", m_availableRuntimes.size());
}

std::filesystem::path JavaManager::getExtractionPathForRuntime(const JavaVersion& javaVersion) {
    // Naming convention: source_component_majorVersion
    // This needs to be consistent with how runtimeNameForPath is generated in ensureJavaForMinecraftVersion
    // For now, directly use component and majorVersion for simplicity, source can be added if needed.
    std::string dir_name = javaVersion.component + "_" + std::to_string(javaVersion.majorVersion);
    return m_config.javaRuntimesDir / dir_name;
}

std::optional<JavaRuntime> JavaManager::ensureJavaForMinecraftVersion(const Version& mcVersion) {
    m_logger->info("Ensuring Java for Minecraft version: {}", mcVersion.id);
    if (!mcVersion.javaVersion) {
        m_logger->warn("Minecraft version {} does not specify a Java version. Cannot automatically ensure Java.", mcVersion.id);
        return std::nullopt;
    }

    const auto& requiredJava = *mcVersion.javaVersion;
    m_logger->info("Required Java: Component '{}', Major Version '{}'", requiredJava.component, requiredJava.majorVersion);

    for (const auto& runtime : m_availableRuntimes) {
        // Make comparison more robust, e.g. what if component name has source prefix already?
        // For now, this relies on scanForExistingRuntimes parsing it correctly.
        if (runtime.componentName == requiredJava.component && runtime.majorVersion == requiredJava.majorVersion) {
            m_logger->info("Found existing suitable Java runtime: {}", runtime.homePath.string());
            return runtime;
        }
    }
    m_logger->info("No existing suitable Java runtime found for {} v{}. Attempting download.",
              requiredJava.component, requiredJava.majorVersion);

    std::filesystem::path downloadedArchivePath;
    std::string sourceApi = "unknown";

    std::filesystem::path adoptiumDownloadDir = m_config.javaRuntimesDir / "_downloads" / "adoptium";
    downloadedArchivePath = m_javaDownloader.downloadJavaForSpecificVersionAdoptium(requiredJava, adoptiumDownloadDir);
    if (!downloadedArchivePath.empty()) {
        sourceApi = "adoptium";
    } else {
        m_logger->warn("Adoptium download failed or not suitable. Trying Mojang manifest...");
        std::filesystem::path mojangDownloadDir = m_config.javaRuntimesDir / "_downloads" / "mojang";
        downloadedArchivePath = m_javaDownloader.downloadJavaForMinecraftVersionMojang(mcVersion, mojangDownloadDir);
        if (!downloadedArchivePath.empty()) {
            sourceApi = "mojang";
        }
    }

    if (downloadedArchivePath.empty()) {
        m_logger->error("Failed to download Java for {} v{}", requiredJava.component, requiredJava.majorVersion);
        return std::nullopt;
    }

    m_logger->info("Java archive downloaded via {} to: {}", sourceApi, downloadedArchivePath.string());

    // Construct runtimeNameForPath based on how directories are named.
    // If scanForExistingRuntimes expects "source_component_version", then use that.
    // Or, if getExtractionPathForRuntime uses just "component_version", align here.
    // Let's use what getExtractionPathForRuntime would produce:
    std::filesystem::path extractionTargetDir = getExtractionPathForRuntime(requiredJava);
    std::string runtimeNameForPath = extractionTargetDir.filename().string(); // e.g. "java-runtime-gamma_17"

    if (extractJavaArchive(downloadedArchivePath, extractionTargetDir, runtimeNameForPath)) {
        m_logger->info("Java archive extracted to: {}", extractionTargetDir.string());

        // findJavaExecutable needs the *actual* home of the JRE/JDK after extraction
        // which might be a subdirectory within extractionTargetDir
        std::filesystem::path javaExePath = findJavaExecutable(extractionTargetDir);

        if (!javaExePath.empty()) {
            // The 'homePath' for JavaRuntime should be the directory containing 'bin', 'lib', etc.
            // findJavaExecutable should ideally return this effective home or we deduce it.
            // For now, assume extractionTargetDir or a direct subdir is the effective home.
            // A more robust way is for findJavaExecutable to return the effective home path.
            std::filesystem::path effectiveHome = javaExePath.parent_path().parent_path(); // .. from bin/java
            if (!std::filesystem::exists(effectiveHome / "lib")) { // A simple check
                effectiveHome = extractionTargetDir; // Fallback if structure is unexpected
                 m_logger->warn("Could not reliably determine effective Java home from executable path {}. Using extraction target {}.", javaExePath.string(), extractionTargetDir.string());
            }


            JavaRuntime newRuntime = {effectiveHome, javaExePath, requiredJava.majorVersion, requiredJava.component, sourceApi};
            m_availableRuntimes.push_back(newRuntime);

            // Remove the downloaded archive
            std::error_code ec_remove;
            std::filesystem::remove(downloadedArchivePath, ec_remove);
            if(ec_remove) {
                m_logger->warn("Failed to remove downloaded archive {}: {}", downloadedArchivePath.string(), ec_remove.message());
            } else {
                m_logger->info("Removed downloaded archive: {}", downloadedArchivePath.string());
            }
            return newRuntime;
        } else {
            m_logger->error("Failed to find Java executable in {}", extractionTargetDir.string());
        }
    } else {
        m_logger->error("Failed to extract Java archive {}", downloadedArchivePath.string());
    }

    if(std::filesystem::exists(downloadedArchivePath)) {
        std::filesystem::remove(downloadedArchivePath);
        m_logger->info("Cleaned up downloaded archive after failure: {}", downloadedArchivePath.string());
    }
    return std::nullopt;
}

bool JavaManager::extractJavaArchive(const std::filesystem::path& archivePath, const std::filesystem::path& extractionDir, const std::string& runtimeNameForPath) {
    m_logger->info("Attempting to extract archive {} to {}", archivePath.string(), extractionDir.string());
    try {
        if (std::filesystem::exists(extractionDir)) {
            m_logger->info("Extraction directory {} already exists. Removing for fresh extraction.", extractionDir.string());
            std::filesystem::remove_all(extractionDir);
        }
        std::filesystem::create_directories(extractionDir);

        miniz_cpp::zip_file zipFile;
        zipFile.load(archivePath.string());

        m_logger->info("Extracting {} files from {} to {}...", zipFile.infolist().size(), archivePath.string(), extractionDir.string());
        zipFile.extractall(extractionDir.string());

        m_logger->info("Extraction complete for {}.", archivePath.string());
        return true;
    } catch (const std::exception& e) {
        m_logger->error("Error extracting archive {}: {}", archivePath.string(), e.what());
        if (std::filesystem::exists(extractionDir)) {
            std::filesystem::remove_all(extractionDir);
        }
        return false;
    }
}

std::filesystem::path JavaManager::findJavaExecutable(const std::filesystem::path& extractedJavaBaseDir) {
    m_logger->trace("Attempting to find Java executable in/under: {}", extractedJavaBaseDir.string());

    std::filesystem::path currentSearchDir = extractedJavaBaseDir;

    // Archives often extract into a single root folder. If extractedJavaBaseDir has one subdir, search in it.
    if (std::filesystem::exists(extractedJavaBaseDir) && std::filesystem::is_directory(extractedJavaBaseDir)) {
        std::vector<std::filesystem::path> subdirs;
        for (const auto& entry : std::filesystem::directory_iterator(extractedJavaBaseDir)) {
            if (entry.is_directory()) {
                subdirs.push_back(entry.path());
            }
        }
        if (subdirs.size() == 1) {
            // Check if this single subdir looks like a Java home (e.g., contains "bin" or "release" file)
            if (std::filesystem::exists(subdirs[0] / "bin") || std::filesystem::exists(subdirs[0] / "release")) {
                 m_logger->trace("Archive extracted into a root folder: {}. Searching within.", subdirs[0].filename().string());
                currentSearchDir = subdirs[0];
            }
        } else if (subdirs.empty()) {
            // No subdirectories, currentSearchDir is already the base.
             m_logger->trace("No subdirectories in {}, searching directly.", extractedJavaBaseDir.string());
        } else {
            // Multiple subdirectories, this is unusual for a JRE/JDK archive root.
            // We'll search currentSearchDir (which is extractedJavaBaseDir), and if that fails, we could iterate subdirs.
            // For now, let's keep it simple and assume the top level or a single subdir.
            m_logger->trace("Multiple subdirectories in {}, searching directly in base first.", extractedJavaBaseDir.string());
        }
    } else {
        m_logger->error("Provided Java base directory {} does not exist or is not a directory.", extractedJavaBaseDir.string());
        return "";
    }


    std::filesystem::path binDir = currentSearchDir / "bin";

    // Special handling for macOS .tar.gz from Adoptium (often has Contents/Home/bin)
    if (Utils::getCurrentOS() == Utils::OperatingSystem::MACOS) {
        std::filesystem::path macOSBinDir = currentSearchDir / "Contents" / "Home" / "bin";
        if (std::filesystem::exists(macOSBinDir) && std::filesystem::is_directory(macOSBinDir)) {
            binDir = macOSBinDir;
            m_logger->trace("Using macOS specific JRE structure: {}", binDir.string());
        }
    }

    if (!std::filesystem::exists(binDir) || !std::filesystem::is_directory(binDir)) {
        m_logger->error("'bin' directory not found in potential Java home: {}", currentSearchDir.string());
        return "";
    }

    std::filesystem::path javaExePath;
    #if defined(_WIN32) || defined(_WIN64)
        javaExePath = binDir / "javaw.exe";
        if (!std::filesystem::exists(javaExePath) || !std::filesystem::is_regular_file(javaExePath)) {
            m_logger->trace("javaw.exe not found in {}, trying java.exe", binDir.string());
            javaExePath = binDir / "java.exe";
        }
    #else
        javaExePath = binDir / "java";
    #endif

    if (std::filesystem::exists(javaExePath) && std::filesystem::is_regular_file(javaExePath)) {
        m_logger->info("Found Java executable: {}", javaExePath.string());
        return javaExePath;
    }

    m_logger->error("Java executable not found in {}", binDir.string());
    return "";
}


void JavaManager::scanForExistingRuntimes() {
    m_availableRuntimes.clear();
    if (!std::filesystem::exists(m_config.javaRuntimesDir) || !std::filesystem::is_directory(m_config.javaRuntimesDir)) {
        m_logger->warn("Java runtimes directory {} does not exist or is not a directory. Cannot scan.", m_config.javaRuntimesDir.string());
        return;
    }

    m_logger->info("Scanning for existing Java runtimes in {}...", m_config.javaRuntimesDir.string());
    for (const auto& entry : std::filesystem::directory_iterator(m_config.javaRuntimesDir)) {
        if (entry.is_directory() && entry.path().filename().string().rfind("_downloads", 0) != 0) { // Ignore directories starting with _downloads
            std::filesystem::path javaHomeCandidate = entry.path();
            m_logger->trace("Scanning potential Java home: {}", javaHomeCandidate.string());

            std::filesystem::path javaExe = findJavaExecutable(javaHomeCandidate);

            if (!javaExe.empty()) {
                std::string dirName = javaHomeCandidate.filename().string();
                std::string source = "unknown";
                std::string component = "unknown";
                unsigned int majorVersion = 0;

                // Try to parse: source_component_majVer or just component_majVer
                size_t last_underscore = dirName.rfind('_');
                if (last_underscore != std::string::npos) {
                    try {
                        majorVersion = std::stoul(dirName.substr(last_underscore + 1));
                        std::string prefix = dirName.substr(0, last_underscore);
                        size_t first_underscore = prefix.find('_');
                        if (first_underscore != std::string::npos) {
                            source = prefix.substr(0, first_underscore);
                            component = prefix.substr(first_underscore + 1);
                        } else {
                            component = prefix; // Assume no source prefix, just component_majVer
                            source = "user"; // Or some other default if no source found
                        }
                    } catch (const std::exception& e) {
                        m_logger->warn("Could not parse major version from directory name '{}': {}", dirName, e.what());
                         majorVersion = 0; // Reset if parsing failed
                    }
                } else {
                     m_logger->warn("Could not parse runtime details from directory name: {}", dirName);
                }

                if (majorVersion > 0 && component != "unknown") {
                    // The effective home path should be the directory containing bin, lib etc.
                    // findJavaExecutable found the exe, so its parent's parent is the home.
                    std::filesystem::path effectiveHome = javaExe.parent_path().parent_path();
                    m_logger->info("Discovered existing runtime: Component '{}', Version '{}' (Source: '{}') at {}", component, majorVersion, source, effectiveHome.string());
                    m_availableRuntimes.push_back({effectiveHome, javaExe, majorVersion, component, source});
                } else {
                    m_logger->warn("Found Java executable in {} but could not determine full details from directory name. Skipping.", javaHomeCandidate.string());
                }
            } else {
                 m_logger->trace("No Java executable found in candidate directory: {}", javaHomeCandidate.string());
            }
        }
    }
    m_logger->info("Scan complete. Found {} usable existing runtimes.", m_availableRuntimes.size());
}


std::vector<JavaRuntime> JavaManager::getAvailableRuntimes() const {
    return m_availableRuntimes;
}


} // namespace Launcher