// src/JavaManager.cpp
#include <Launcher/JavaManager.hpp>
#include <Launcher/HttpManager.hpp>
#include <Launcher/JavaDownloader.hpp>
#include <Launcher/Utils/OS.hpp>
#include <Launcher/Utils/Logger.hpp>
#include <fstream>
#include <algorithm>
#include <vector> // For buffer in extraction

// Minizip-NG includes
extern "C" { // minizip-ng headers are C headers
#include <mz.h>
#include <mz_os.h>
#include <mz_strm.h>
#include <mz_strm_os.h>
#include <mz_zip.h>
#include <mz_zip_rw.h>
}


namespace Launcher {

// Constructor and other methods (getExtractionPathForRuntime, ensureJavaForMinecraftVersion, findJavaExecutable, scanForExistingRuntimes)
// remain largely the same as the previous "full code" version, except they will call the new extractJavaArchive.
// For brevity, I'm only showing the modified/new extractJavaArchive and its helpers.

JavaManager::JavaManager(const Config& config, HttpManager& httpManager)
    : m_config(config),
      m_httpManager(httpManager),
      m_javaDownloader(m_httpManager) {
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
    std::string dir_name = javaVersion.component + "_" + std::to_string(javaVersion.majorVersion);
    return m_config.javaRuntimesDir / dir_name;
}


// Helper function for minizip-ng extraction
static int32_t minizip_extract_callback(void *handle, void *userdata, mz_zip_file *file_info, const char *path) {
    std::filesystem::path& extraction_dir = *static_cast<std::filesystem::path*>(userdata);
    std::filesystem::path target_path = extraction_dir / std::filesystem::path(file_info->filename).lexically_normal();

    std::shared_ptr<spdlog::logger> logger = Utils::Logger::GetOrCreateLogger("JavaManagerExtract"); // Or pass logger

    if (mz_zip_entry_is_dir(file_info) == MZ_OK) {
        if (!std::filesystem::exists(target_path)) {
            if (!std::filesystem::create_directories(target_path)) {
                logger->error("Failed to create directory: {}", target_path.string());
                return MZ_INTERNAL_ERROR;
            }
        }
    } else {
        // Ensure parent directory exists
        if (target_path.has_parent_path() && !std::filesystem::exists(target_path.parent_path())) {
            if (!std::filesystem::create_directories(target_path.parent_path())) {
                 logger->error("Failed to create parent directory for file: {}", target_path.string());
                return MZ_INTERNAL_ERROR;
            }
        }
        // Extract the file
        if (mz_zip_entry_read(handle        ) != MZ_OK) {
            logger->error("Failed to extract file: {} to {}", std::string(file_info->filename), target_path.string());
            return MZ_INTERNAL_ERROR;
        }
    }
    return MZ_OK;
}


bool JavaManager::extractJavaArchive(const std::filesystem::path& archivePath, const std::filesystem::path& extractionDir, const std::string& /*runtimeNameForPath*/) {
    m_logger->info("Attempting to extract archive {} to {} using minizip-ng.", archivePath.string(), extractionDir.string());

    void *zip_reader = nullptr;
    int32_t err = MZ_OK;

    try {
        if (std::filesystem::exists(extractionDir)) {
            m_logger->info("Extraction directory {} already exists. Removing for fresh extraction.", extractionDir.string());
            std::filesystem::remove_all(extractionDir);
        }
        if (!std::filesystem::create_directories(extractionDir)) {
            m_logger->error("Failed to create base extraction directory: {}", extractionDir.string());
            return false;
        }

        zip_reader = mz_zip_reader_create();
        if (!zip_reader) {
            m_logger->error("Failed to create zip_reader handle.");
            return false;
        }

        // Convert path to UTF-8 for minizip-ng functions that expect char*
        std::string archivePathStr = archivePath.string();
        std::string extractionDirStr = extractionDir.string();

        err = mz_zip_reader_open_file(zip_reader, archivePathStr.c_str());
        if (err != MZ_OK) {
            m_logger->error("Failed to open ZIP archive {}: mz_zip_reader_open_file error {}", archivePathStr, err);
            mz_zip_reader_delete(&zip_reader);
            return false;
        }

        m_logger->info("Extracting files from {} to {}...", archivePathStr, extractionDirStr);

        // Iterate and extract all files
        // Option 1: Iterate and extract one by one (more control, more complex)
        // Option 2: Use a higher-level extraction function if minizip-ng provides one that fits
        // For now, let's try mz_zip_reader_extract_all_cb which seems suitable

        // We need to pass the extractionDir as userdata to the callback
        std::filesystem::path user_extraction_dir = extractionDir;
        err = mz_zip_reader_extract_all_cb(zip_reader, extractionDirStr.c_str(), minizip_extract_callback, &user_extraction_dir);

        if (err != MZ_OK) {
            m_logger->error("Failed to extract all files from {}: mz_zip_reader_extract_all_cb error {}", archivePathStr, err);
            // Cleanup might be partial, so let's not delete extractionDir immediately here unless sure.
        } else {
            m_logger->info("Extraction complete for {}.", archivePath.string());
        }

        mz_zip_reader_close(zip_reader);
        mz_zip_reader_delete(&zip_reader);

        return (err == MZ_OK);

    } catch (const std::exception& e) {
        m_logger->error("Exception during archive extraction {}: {}", archivePath.string(), e.what());
        if (zip_reader) {
            mz_zip_reader_close(zip_reader);
            mz_zip_reader_delete(&zip_reader);
        }
        // Cleanup partially extracted directory
        if (std::filesystem::exists(extractionDir)) {
             std::error_code ec_remove;
            std::filesystem::remove_all(extractionDir, ec_remove);
             if(ec_remove) m_logger->warn("Failed to cleanup extraction directory {} after exception: {}", extractionDir.string(), ec_remove.message());
        }
        return false;
    }
}

// findJavaExecutable, ensureJavaForMinecraftVersion, scanForExistingRuntimes, getAvailableRuntimes
// remain the same as the previous "full code" response where we refined findJavaExecutable.
// I will include them here for completeness.

std::filesystem::path JavaManager::findJavaExecutable(const std::filesystem::path& extractionBaseDir) {
    m_logger->trace("Attempting to find Java executable in/under base extraction directory: {}", extractionBaseDir.string());

    std::filesystem::path javaHomePath = extractionBaseDir;

    if (!std::filesystem::exists(extractionBaseDir) || !std::filesystem::is_directory(extractionBaseDir)) {
        m_logger->error("Provided Java base directory {} does not exist or is not a directory.", extractionBaseDir.string());
        return "";
    }

    std::filesystem::path potentialBinDir = extractionBaseDir / "bin";
    bool foundBinDirectly = std::filesystem::exists(potentialBinDir) && std::filesystem::is_directory(potentialBinDir);

    if (!foundBinDirectly) {
        m_logger->trace("'bin' not directly under {}. Looking for a suitable subdirectory.", extractionBaseDir.string());
        std::vector<std::filesystem::path> subdirs;
        for (const auto& entry : std::filesystem::directory_iterator(extractionBaseDir)) {
            if (entry.is_directory()) {
                subdirs.push_back(entry.path());
            }
        }

        if (subdirs.size() == 1) {
            m_logger->info("Found single subdirectory '{}' in extraction path. Assuming it's the Java home.", subdirs[0].filename().string());
            javaHomePath = subdirs[0];
        } else if (subdirs.size() > 1) {
            m_logger->warn("Multiple subdirectories found in {}. Attempting to find a likely Java home.", extractionBaseDir.string());
            bool foundLikelyHome = false;
            for (const auto& subdir : subdirs) {
                if (std::filesystem::exists(subdir / "bin") && std::filesystem::is_directory(subdir / "bin")) {
                    m_logger->info("Found likely Java home in subdirectory: {}", subdir.string());
                    javaHomePath = subdir;
                    foundLikelyHome = true;
                    break;
                }
                if (std::filesystem::exists(subdir / "release") && std::filesystem::is_regular_file(subdir / "release")) {
                     m_logger->info("Found 'release' file in subdirectory, assuming Java home: {}", subdir.string());
                     javaHomePath = subdir;
                     foundLikelyHome = true;
                     break;
                }
            }
            if (!foundLikelyHome) {
                m_logger->error("Could not determine the correct Java home among multiple subdirectories in {}.", extractionBaseDir.string());
                return "";
            }
        } else {
            m_logger->error("No subdirectories and no 'bin' directory found directly in {}.", extractionBaseDir.string());
            return "";
        }
    } else {
        m_logger->trace("'bin' directory found directly under {}. Using this as Java home.", extractionBaseDir.string());
    }

    m_logger->trace("Effective Java home path for searching 'bin': {}", javaHomePath.string());
    std::filesystem::path binDir = javaHomePath / "bin";

    if (Utils::getCurrentOS() == Utils::OperatingSystem::MACOS) {
        std::filesystem::path macOSBinDir = javaHomePath / "Contents" / "Home" / "bin";
        if (std::filesystem::exists(macOSBinDir) && std::filesystem::is_directory(macOSBinDir)) {
            binDir = macOSBinDir;
            m_logger->info("Using macOS specific JRE structure for bin: {}", binDir.string());
        }
    }

    if (!std::filesystem::exists(binDir) || !std::filesystem::is_directory(binDir)) {
        m_logger->error("'bin' directory not found in resolved Java home: {}", javaHomePath.string());
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

std::optional<JavaRuntime> JavaManager::ensureJavaForMinecraftVersion(const Version& mcVersion) {
    m_logger->info("Ensuring Java for Minecraft version: {}", mcVersion.id);
    if (!mcVersion.javaVersion) {
        m_logger->warn("Minecraft version {} does not specify a Java version. Cannot automatically ensure Java.", mcVersion.id);
        return std::nullopt;
    }

    const auto& requiredJava = *mcVersion.javaVersion;
    m_logger->info("Required Java: Component '{}', Major Version '{}'", requiredJava.component, requiredJava.majorVersion);

    for (const auto& runtime : m_availableRuntimes) {
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

    std::filesystem::path extractionTargetDir = getExtractionPathForRuntime(requiredJava);
    std::string runtimeNameForPath = extractionTargetDir.filename().string();

    if (extractJavaArchive(downloadedArchivePath, extractionTargetDir, runtimeNameForPath)) {
        m_logger->info("Java archive extracted to: {}", extractionTargetDir.string());

        std::filesystem::path javaExePath = findJavaExecutable(extractionTargetDir);

        if (!javaExePath.empty()) {
            std::filesystem::path effectiveJavaHome = javaExePath.parent_path().parent_path();

            JavaRuntime newRuntime = {effectiveJavaHome, javaExePath, requiredJava.majorVersion, requiredJava.component, sourceApi};
            m_availableRuntimes.push_back(newRuntime);

            m_logger->info("Successfully configured Java runtime: Component={}, Version={}, Source={}, Home='{}', Executable='{}'",
                newRuntime.componentName, newRuntime.majorVersion, newRuntime.source, newRuntime.homePath.string(), newRuntime.javaExecutablePath.string());

            std::error_code ec_remove;
            std::filesystem::remove(downloadedArchivePath, ec_remove);
            if(ec_remove) {
                m_logger->warn("Failed to remove downloaded archive {}: {}", downloadedArchivePath.string(), ec_remove.message());
            } else {
                m_logger->info("Removed downloaded archive: {}", downloadedArchivePath.string());
            }
            return newRuntime;
        } else {
            m_logger->error("Failed to find Java executable in the extracted archive at {}", extractionTargetDir.string());
        }
    } else {
        m_logger->error("Failed to extract Java archive {}", downloadedArchivePath.string());
    }

    if(std::filesystem::exists(downloadedArchivePath)) {
        std::error_code ec_remove_fail;
        std::filesystem::remove(downloadedArchivePath, ec_remove_fail);
        if(ec_remove_fail) {
             m_logger->warn("Cleanup: Failed to remove archive {} after failure: {}", downloadedArchivePath.string(), ec_remove_fail.message());
        } else {
             m_logger->info("Cleaned up downloaded archive after failure: {}", downloadedArchivePath.string());
        }
    }
    return std::nullopt;
}

void JavaManager::scanForExistingRuntimes() {
    m_availableRuntimes.clear();
    if (!std::filesystem::exists(m_config.javaRuntimesDir) || !std::filesystem::is_directory(m_config.javaRuntimesDir)) {
        m_logger->warn("Java runtimes directory {} does not exist or is not a directory. Cannot scan.", m_config.javaRuntimesDir.string());
        return;
    }

    m_logger->info("Scanning for existing Java runtimes in {}...", m_config.javaRuntimesDir.string());
    for (const auto& entry : std::filesystem::directory_iterator(m_config.javaRuntimesDir)) {
        if (entry.is_directory() && entry.path().filename().string().rfind("_downloads", 0) != 0) {
            std::filesystem::path extractionCandidateDir = entry.path();
            m_logger->trace("Scanning potential Java extraction directory: {}", extractionCandidateDir.string());

            std::filesystem::path javaExe = findJavaExecutable(extractionCandidateDir);

            if (!javaExe.empty()) {
                std::string dirName = extractionCandidateDir.filename().string();
                std::string source = "unknown";
                std::string component = "unknown";
                unsigned int majorVersion = 0;

                size_t last_underscore = dirName.rfind('_');
                if (last_underscore != std::string::npos && last_underscore < dirName.length() - 1) {
                    try {
                        majorVersion = std::stoul(dirName.substr(last_underscore + 1));
                        std::string prefix = dirName.substr(0, last_underscore);
                        size_t first_underscore = prefix.find('_');
                        if (first_underscore != std::string::npos && first_underscore < prefix.length() -1 ) {
                            source = prefix.substr(0, first_underscore);
                            component = prefix.substr(first_underscore + 1);
                        } else {
                            component = prefix;
                            source = "user_provided";
                        }
                    } catch (const std::exception& e) {
                        m_logger->warn("Could not parse major version from directory name '{}': {}", dirName, e.what());
                         majorVersion = 0;
                    }
                } else {
                     m_logger->warn("Could not parse runtime details from directory name (expected '[source_]component_version'): {}", dirName);
                }

                if (majorVersion > 0 && component != "unknown" && !component.empty()) {
                    std::filesystem::path effectiveJavaHome = javaExe.parent_path().parent_path();
                    m_logger->info("Discovered existing runtime: Component '{}', Version '{}' (Source: '{}') at Home='{}', Exe='{}'",
                        component, majorVersion, source, effectiveJavaHome.string(), javaExe.string());
                    m_availableRuntimes.push_back({effectiveJavaHome, javaExe, majorVersion, component, source});
                } else {
                    m_logger->warn("Found Java executable in {} but could not determine full details from directory name '{}'. Skipping.",
                        extractionCandidateDir.string(), dirName);
                }
            } else {
                 m_logger->trace("No Java executable found in candidate directory structure: {}", extractionCandidateDir.string());
            }
        }
    }
    m_logger->info("Scan complete. Found {} usable existing runtimes.", m_availableRuntimes.size());
}

std::vector<JavaRuntime> JavaManager::getAvailableRuntimes() const {
    return m_availableRuntimes;
}

} // namespace Launcher