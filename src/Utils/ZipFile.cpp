// src/Utils/ZipFile.cpp
#include <Launcher/Utils/Logger.hpp> // For GetOrCreateLogger
#include <Launcher/Utils/ZipFile.hpp>

#include "mz_strm_mem.h"

// Minizip-ng headers
extern "C" { // If minizip headers are pure C or have C linkage issues
    #include "mz.h"
    #include "mz_os.h"
    // #include "mz_strm.h" // May not be needed for basic file extraction
    // #include "mz_strm_os.h" // May not be needed for basic file extraction
    #include "mz_zip.h"
    #include "mz_zip_rw.h"
}

#include <vector> // For buffer in extractFile if implemented

#ifdef _WIN32
#include <direct.h>
#define MKDIR_IMPL(path_str) _mkdir(path_str)
#else
#include <sys/stat.h>
#define MKDIR_IMPL(path_str) mkdir(path_str, 0755)
#endif


namespace Launcher::Utils {

    ZipFile::ZipFile(const std::filesystem::path &archivePath) : m_archivePath(archivePath), m_zipReader(nullptr) {
        m_logger = Logger::GetOrCreateLogger("ZipFile");
        m_zipReader = mz_zip_reader_create();
        if (!m_zipReader) {
            m_lastErrorMsg = "Failed to create zip reader instance.";
            m_logger->error("[{}] {}", m_archivePath.filename().string(), m_lastErrorMsg);
        }
    }

    ZipFile::~ZipFile() {
        if (m_zipReader) {
            if (isOpen()) { // Check if it was successfully opened before trying to close
                mz_zip_reader_close(m_zipReader);
            }
            mz_zip_reader_delete(&m_zipReader); // m_zipReader will be set to NULL
            m_logger->trace("[{}] Zip reader deleted.", m_archivePath.filename().string());
        }
    }

    void ZipFile::logMzError(void *err, const std::string &context) {
        m_lastErrorMsg = context + ": Minizip-ng error " + std::to_string(err);
        m_logger->error("[{}] {}", m_archivePath.filename().string(), m_lastErrorMsg);
    }


    bool ZipFile::open() {
        if (!m_zipReader) {
            m_lastErrorMsg = "Zip reader was not created.";
            // Logger already logged this in constructor
            return false;
        }
        if (isOpen()) { // If already open, perhaps close and reopen or just return true
            m_logger->trace("[{}] Archive already open.", m_archivePath.filename().string());
            return true;
        }

        m_logger->info("[{}] Opening archive...", m_archivePath.filename().string());
        int32_t err = mz_zip_reader_open_file(m_zipReader, m_archivePath.string().c_str());
        if (err != MZ_OK) {
            logMzError(err, "Failed to open zip file");
            // mz_zip_reader_delete(&m_zipReader); // Don't delete here, destructor handles it
            return false;
        }
        m_logger->info("[{}] Archive opened successfully.", m_archivePath.filename().string());
        return true;
    }

    bool ZipFile::isOpen() const {
        if (!m_zipReader)
            return false;
        // Check if an operation that requires an open state was successful,
        // e.g., trying to get number of entries or go to first entry.
        // mz_zip_reader_is_open() is available in later Minizip-ng versions, check your version.
        // For now, we assume if mz_zip_reader_open_file was OK, it's "open enough".
        // A more robust check would be to see if mz_zip_reader_get_zip_cd is non-null or similar.
        // Or, simpler: just rely on the success of mz_zip_reader_open_file.
        // Let's consider it open if mz_zip_reader_open_file hasn't failed and m_zipReader is valid.
        // The mz_zip_reader_get_state might be useful but is more internal.
        return mz_zip_reader_get_pattern(m_zipReader, NULL, NULL) ==
               MZ_OK; // A simple check that requires an open archive
    }

    std::string ZipFile::getLastError() const { return m_lastErrorMsg; }

    void ZipFile::ensureDirectoryExists(const std::filesystem::path &path) {
        if (path.empty())
            return;

        if (!std::filesystem::exists(path)) {
            std::error_code ec;
            if (std::filesystem::create_directories(path, ec)) {
                m_logger->trace("[{}] Created directory: {}", m_archivePath.filename().string(), path.string());
            } else {
                m_lastErrorMsg = "Failed to create directory " + path.string() + ": " + ec.message();
                m_logger->error("[{}] {}", m_archivePath.filename().string(), m_lastErrorMsg);
                // Depending on severity, you might throw an exception here
            }
        }
    }


    bool ZipFile::extractAll(const std::filesystem::path &outputDirectory) {
        if (!m_zipReader) {
            m_lastErrorMsg = "Zip reader not initialized.";
            m_logger->error("[{}] {}", m_archivePath.filename().string(), m_lastErrorMsg);
            return false;
        }
        if (!isOpen()) {
            if (!open()) { // Try to open if not already
                return false; // open() will set m_lastErrorMsg and log
            }
        }

        m_logger->info("[{}] Starting extraction to: {}", m_archivePath.filename().string(), outputDirectory.string());
        ensureDirectoryExists(outputDirectory);

        int32_t err = mz_zip_reader_goto_first_entry(m_zipReader);
        if (err != MZ_OK && err != MZ_END_OF_LIST) {
            logMzError(err, "Failed to go to first entry");
            return false;
        }

        bool all_successful = true;
        while (err == MZ_OK) {
            mz_zip_file *file_info = nullptr;
            err = mz_zip_reader_entry_get_info(m_zipReader, &file_info);
            if (err != MZ_OK) {
                logMzError(err, "Failed to get entry info");
                all_successful = false;
                break;
            }

            std::filesystem::path entry_filename_path =
                    std::filesystem::path(file_info->filename);
            std::filesystem::path output_path = outputDirectory / entry_filename_path;

            m_logger->trace("[{}] Processing entry: {}", m_archivePath.filename().string(),
                            entry_filename_path.string());

            if (mz_zip_reader_entry_is_dir(m_zipReader) == MZ_OK) {
                m_logger->trace("[{}] Creating directory: {}", m_archivePath.filename().string(), output_path.string());
                ensureDirectoryExists(output_path);
            } else {
                std::filesystem::path parent_dir = output_path.parent_path();
                if (!parent_dir.empty()) {
                    ensureDirectoryExists(parent_dir);
                }

                m_logger->trace("[{}] Extracting file to: {}", m_archivePath.filename().string(), output_path.string());
                err = mz_zip_reader_entry_save_file(m_zipReader, output_path.string().c_str());
                if (err != MZ_OK) {
                    logMzError(err,
                               "Failed to save entry " + entry_filename_path.string() + " to " + output_path.string());
                    all_successful = false;
                    // Optionally: continue to next entry or break
                }
            }

            // Free file_info (mz_zip_reader_entry_get_info allocates it)
            if (file_info) {
                mz_zip_entry_delete(&file_info); // Important to free this!
            }

            if (err != MZ_OK) { // If saving or dir creation had an error, might affect next step
                // We've already logged the specific error.
                // Decide if we want to stop entirely or try to continue. For now, let's try to continue.
                all_successful = false;
            }

            err = mz_zip_reader_goto_next_entry(m_zipReader);
        }

        if (err == MZ_END_OF_LIST) {
            m_logger->info("[{}] Finished extracting all entries.", m_archivePath.filename().string());
        } else if (err != MZ_OK) {
            // This means the loop terminated due to an error in goto_next_entry or a prior error.
            logMzError(err, "An error occurred during entry traversal");
            all_successful = false;
        }

        return all_successful;
    }

} // namespace Launcher::Utils
