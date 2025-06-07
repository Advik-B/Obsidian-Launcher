// include/Launcher/Utils/ZipFile.hpp
#ifndef ZIP_FILE_UTIL_HPP
#define ZIP_FILE_UTIL_HPP

#include <filesystem>
#include <string>
#include <vector>
#include <spdlog/logger.h>

namespace Launcher::Utils {

    class ZipFile {
    public:
        ZipFile(const std::filesystem::path &archivePath);
        ~ZipFile();

        // Attempts to open the zip file. Returns true on success.
        bool open();

        // Extracts all entries from the opened zip file to the specified output directory.
        // Returns true if all entries were extracted successfully (or if there were no show-stopping errors).
        bool extractAll(const std::filesystem::path &outputDirectory);

        // Optional: Get a list of filenames in the archive
        // std::vector<std::string> getFilenames();

        // Optional: Extract a single file
        // bool extractFile(const std::string& filenameInArchive, const std::filesystem::path& outputPath);

        bool isOpen() const;
        std::string getLastError() const;


    private:
        std::filesystem::path m_archivePath;
        void *m_zipReader; // Opaque pointer to mz_zip_reader
        std::shared_ptr<spdlog::logger> m_logger;
        std::string m_lastErrorMsg;

        void ensureDirectoryExists(const std::filesystem::path &path);
        void logMzError(void *err, const std::string &context);
    };

} // namespace Launcher::Utils

#endif // ZIP_FILE_UTIL_HPP