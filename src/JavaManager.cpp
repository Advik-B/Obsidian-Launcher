// src/JavaManager.cpp
#include <Launcher/JavaManager.hpp>
#include <Launcher/Utils/OS.hpp>     // For OS specific executable names
#include <miniz_cpp.hpp>             // For ZIP extraction
#include <iostream>
#include <fstream>
#include <algorithm> // For std::remove_if if cleaning up extraction path

namespace Launcher {

JavaManager::JavaManager(const Config& config) : m_config(config), m_javaDownloader() {
    if (!std::filesystem::exists(m_config.javaRuntimesDir)) {
        std::filesystem::create_directories(m_config.javaRuntimesDir);
    }
    scanForExistingRuntimes(); // Populate m_availableRuntimes
}

std::filesystem::path JavaManager::getExtractionPathForRuntime(const JavaVersion& javaVersion) {
    // Creates a path like: ./java_runtimes/mojang_jre-legacy_17 or ./java_runtimes/adoptium_jre_17
    std::string source_prefix = "unknown"; // Fallback
    // This logic might need refinement based on how JavaDownloader names things or if we want a standard
    // For now, let's use a simpler naming convention just based on component and version
    return m_config.javaRuntimesDir / (javaVersion.component + "_" + std::to_string(javaVersion.majorVersion));
}

std::filesystem::path JavaManager::getExtractionPathForRuntime(const std::string& component, unsigned int majorVersion) {
     return m_config.javaRuntimesDir / (component + "_" + std::to_string(majorVersion));
}


std::optional<JavaRuntime> JavaManager::ensureJavaForMinecraftVersion(const Version& mcVersion) {
    if (!mcVersion.javaVersion) {
        std::cout << "[JavaManager] Minecraft version " << mcVersion.id << " does not specify a Java version. Cannot automatically ensure Java." << std::endl;
        // Here, you might decide to return a default (e.g., system Java or a bundled one) or fail.
        // For now, let's indicate no specific runtime could be ensured.
        return std::nullopt;
    }

    const auto& requiredJava = *mcVersion.javaVersion;

    // Check if we already have a suitable runtime
    for (const auto& runtime : m_availableRuntimes) {
        if (runtime.componentName == requiredJava.component && runtime.majorVersion == requiredJava.majorVersion) {
            std::cout << "[JavaManager] Found existing suitable Java runtime: " << runtime.homePath.string() << std::endl;
            return runtime;
        }
    }
    std::cout << "[JavaManager] No existing suitable Java runtime found for "
              << requiredJava.component << " v" << requiredJava.majorVersion << ". Attempting download." << std::endl;

    std::filesystem::path downloadedArchivePath;
    std::string sourceApi = "unknown";

    // Try Adoptium first (often preferred for broader JRE availability)
    std::filesystem::path adoptiumDownloadDir = m_config.javaRuntimesDir / "_downloads" / "adoptium";
    downloadedArchivePath = m_javaDownloader.downloadJavaForSpecificVersionAdoptium(requiredJava, adoptiumDownloadDir);
    if (!downloadedArchivePath.empty()) {
        sourceApi = "adoptium";
    } else {
        std::cout << "[JavaManager] Adoptium download failed or not suitable. Trying Mojang manifest..." << std::endl;
        std::filesystem::path mojangDownloadDir = m_config.javaRuntimesDir / "_downloads" / "mojang";
        downloadedArchivePath = m_javaDownloader.downloadJavaForMinecraftVersionMojang(mcVersion, mojangDownloadDir);
        if (!downloadedArchivePath.empty()) {
            sourceApi = "mojang";
        }
    }

    if (downloadedArchivePath.empty()) {
        std::cerr << "[JavaManager] Failed to download Java for " << requiredJava.component << " v" << requiredJava.majorVersion << std::endl;
        return std::nullopt;
    }

    std::cout << "[JavaManager] Java archive downloaded via " << sourceApi << " to: " << downloadedArchivePath.string() << std::endl;

    std::string runtimeNameForPath = sourceApi + "_" + requiredJava.component + "_" + std::to_string(requiredJava.majorVersion);
    std::filesystem::path extractionTargetDir = m_config.javaRuntimesDir / runtimeNameForPath;

    if (extractJavaArchive(downloadedArchivePath, extractionTargetDir, runtimeNameForPath)) {
        std::cout << "[JavaManager] Java archive extracted to: " << extractionTargetDir.string() << std::endl;
        std::filesystem::path javaExePath = findJavaExecutable(extractionTargetDir);
        if (!javaExePath.empty()) {
            JavaRuntime newRuntime = {extractionTargetDir, javaExePath, requiredJava.majorVersion, requiredJava.component, sourceApi};
            m_availableRuntimes.push_back(newRuntime); // Add to cache
            // Optionally remove the downloaded archive now
            // std::filesystem::remove(downloadedArchivePath);
            return newRuntime;
        } else {
            std::cerr << "[JavaManager] Failed to find Java executable in " << extractionTargetDir.string() << std::endl;
        }
    } else {
        std::cerr << "[JavaManager] Failed to extract Java archive " << downloadedArchivePath.string() << std::endl;
    }

    // Clean up downloaded archive if extraction or finding executable failed
    if(std::filesystem::exists(downloadedArchivePath)) {
        std::filesystem::remove(downloadedArchivePath);
    }
    return std::nullopt;
}


// Implementation for extractJavaArchive, findJavaExecutable, scanForExistingRuntimes will be added next.
bool JavaManager::extractJavaArchive(const std::filesystem::path& archivePath, const std::filesystem::path& extractionBaseDir, const std::string& runtimeNameForPath) {
    // The final extraction path will be something like: extractionBaseDir / actual_root_folder_in_zip
    // miniz_cpp often extracts with a root folder. We need to find that.
    // Or, more simply for now, just extract directly into extractionBaseDir, assuming user handles cleanup or it's a fresh dir.
    // Let's ensure the target directory is clean or doesn't exist in a conflicting way.

    std::filesystem::path finalExtractionDir = extractionBaseDir; // Simplified for now.
                                                                // Mojang's archives usually contain a single root folder.
                                                                // Adoptium's often do too (e.g. jdk-17.0.7+7-jre)

    try {
        if (std::filesystem::exists(finalExtractionDir)) {
            std::cout << "[JavaManager] Extraction directory " << finalExtractionDir << " already exists. Removing for fresh extraction." << std::endl;
            std::filesystem::remove_all(finalExtractionDir); // Be careful with this!
        }
        std::filesystem::create_directories(finalExtractionDir);

        miniz_cpp::zip_file zipFile;
        zipFile.load(archivePath.string()); // Load from path

        std::cout << "[JavaManager] Extracting " << archivePath.string() << " to " << finalExtractionDir.string() << "..." << std::endl;

        // Miniz-cpp zip_file::extractall extracts files preserving their paths relative to the zip root.
        // It expects the target directory for extraction.
        zipFile.extractall(finalExtractionDir.string());

        std::cout << "[JavaManager] Extraction complete." << std::endl;
        return true;
    } catch (const std::exception& e) {
        std::cerr << "[JavaManager] Error extracting archive " << archivePath.string() << ": " << e.what() << std::endl;
        // Attempt to clean up partially extracted directory
        if (std::filesystem::exists(finalExtractionDir)) {
            std::filesystem::remove_all(finalExtractionDir);
        }
        return false;
    }
}


std::filesystem::path JavaManager::findJavaExecutable(const std::filesystem::path& extractedJavaHome) {
    // Standard Java structure has java/javaw in a 'bin' directory.
    // e.g., jdk-17.0.1/bin/java
    // JREs might be similar: jre-17.0.1/bin/java

    // Need to handle cases where the archive extracts into a subfolder.
    // e.g. archive.zip contains "jdk-17.0.7+7-jre/" and then "bin/java" inside that.
    // `extractedJavaHome` should ideally point to this "jdk-17.0.7+7-jre" like folder.

    std::filesystem::path potentialHome = extractedJavaHome;

    // Heuristic: if extractedJavaHome directly contains a "bin" directory, use it.
    // Otherwise, check if it contains a single subdirectory, and assume that's the actual JRE home.
    if (!std::filesystem::exists(potentialHome / "bin")) {
        std::vector<std::filesystem::path> subdirs;
        for (const auto& entry : std::filesystem::directory_iterator(extractedJavaHome)) {
            if (entry.is_directory()) {
                subdirs.push_back(entry.path());
            }
        }
        if (subdirs.size() == 1) {
            std::cout << "[JavaManager] Archive seems to have a root folder: " << subdirs[0].filename() << ". Adjusting home path." << std::endl;
            potentialHome = subdirs[0];
        } else if (subdirs.size() > 1) {
             // If multiple subdirs, try to find one that looks like a JDK/JRE root (e.g., contains "bin")
            bool found = false;
            for(const auto& subdir : subdirs) {
                if (std::filesystem::exists(subdir / "bin")) {
                    potentialHome = subdir;
                    found = true;
                    std::cout << "[JavaManager] Found potential Java home in subdir: " << potentialHome.string() << std::endl;
                    break;
                }
            }
            if (!found) {
                std::cerr << "[JavaManager] Multiple subdirectories in " << extractedJavaHome << ", and none clearly identifiable as Java home with a 'bin' folder." << std::endl;
                return "";
            }
        }
    }


    std::filesystem::path binDir = potentialHome / "bin";
    if (!std::filesystem::exists(binDir) || !std::filesystem::is_directory(binDir)) {
        std::cerr << "[JavaManager] 'bin' directory not found in " << potentialHome.string() << std::endl;
        return "";
    }

    std::filesystem::path javaExePath;
    #if defined(_WIN32) || defined(_WIN64)
        javaExePath = binDir / "javaw.exe"; // Prefer javaw.exe for no console window
        if (!std::filesystem::exists(javaExePath)) {
            javaExePath = binDir / "java.exe";
        }
    #else
        javaExePath = binDir / "java";
    #endif

    if (std::filesystem::exists(javaExePath) && std::filesystem::is_regular_file(javaExePath)) {
        std::cout << "[JavaManager] Found Java executable: " << javaExePath.string() << std::endl;
        return javaExePath;
    }

    // MacOS .tar.gz from Adoptium might have Contents/Home/bin/java structure
    std::filesystem::path macOsJavaPath = potentialHome / "Contents" / "Home" / "bin" / "java";
    if (Utils::getCurrentOS() == Utils::OperatingSystem::MACOS &&
        std::filesystem::exists(macOsJavaPath) && std::filesystem::is_regular_file(macOsJavaPath)) {
         std::cout << "[JavaManager] Found macOS Java executable: " << macOsJavaPath.string() << std::endl;
        return macOsJavaPath;
    }


    std::cerr << "[JavaManager] Java executable not found in " << binDir.string() << " (or macOS specific path)." << std::endl;
    return "";
}

std::vector<JavaRuntime> JavaManager::getAvailableRuntimes() const {
    return m_availableRuntimes;
}

void JavaManager::scanForExistingRuntimes() {
    m_availableRuntimes.clear();
    if (!std::filesystem::exists(m_config.javaRuntimesDir) || !std::filesystem::is_directory(m_config.javaRuntimesDir)) {
        return;
    }

    std::cout << "[JavaManager] Scanning for existing Java runtimes in " << m_config.javaRuntimesDir.string() << "..." << std::endl;
    for (const auto& entry : std::filesystem::directory_iterator(m_config.javaRuntimesDir)) {
        if (entry.is_directory() && entry.path().filename().string().find("_downloads") == std::string::npos) { // Ignore download cache
            std::filesystem::path javaHome = entry.path();
            std::filesystem::path javaExe = findJavaExecutable(javaHome);
            if (!javaExe.empty()) {
                // Attempt to parse component and version from directory name (e.g., "mojang_jre-legacy_17")
                std::string dirName = javaHome.filename().string();
                // Basic parsing, might need to be more robust
                std::string source = "unknown";
                std::string component = "unknown_component";
                unsigned int majorVersion = 0;

                size_t first_underscore = dirName.find('_');
                if (first_underscore != std::string::npos) {
                    source = dirName.substr(0, first_underscore);
                    size_t second_underscore = dirName.find('_', first_underscore + 1);
                    if (second_underscore != std::string::npos) {
                        component = dirName.substr(first_underscore + 1, second_underscore - (first_underscore + 1));
                        try {
                            majorVersion = std::stoul(dirName.substr(second_underscore + 1));
                        } catch (const std::exception& e) {
                            std::cerr << "[JavaManager] Could not parse major version from directory: " << dirName << std::endl;
                        }
                    }
                }
                 if (majorVersion > 0) { // Only add if we could parse a version
                    std::cout << "[JavaManager] Found existing runtime: " << component << " v" << majorVersion
                              << " (Source: " << source << ") at " << javaHome.string() << std::endl;
                    m_availableRuntimes.push_back({javaHome, javaExe, majorVersion, component, source});
                }
            }
        }
    }
}


} // namespace Launcher