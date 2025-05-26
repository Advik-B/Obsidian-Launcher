#include <Launcher/Utils/Logger.hpp>
#include <spdlog/async.h> // For async logging (optional)
#include <spdlog/sinks/stdout_color_sinks.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <iostream> // For initial error before logger is set up

namespace Launcher {
namespace Utils {

    std::shared_ptr<spdlog::logger> Logger::s_CoreLogger;

    void Logger::Init(const std::filesystem::path& logDir,
                      const std::string& logFileName,
                      spdlog::level::level_enum consoleLevel,
                      spdlog::level::level_enum fileLevel) {
        try {
            std::vector<spdlog::sink_ptr> sinks;

            // Console Sink (thread-safe)
            auto console_sink = std::make_shared<spdlog::sinks::stdout_color_sink_mt>();
            console_sink->set_level(consoleLevel);
            // Example format: [2023-10-27 10:00:00.123] [Launcher] [info] My log message
            console_sink->set_pattern("%^[%Y-%m-%d %H:%M:%S.%e] [%n] [%l] %v%$");
            sinks.push_back(console_sink);

            // File Sink (Rotating, thread-safe)
            if (!logDir.empty() && !logFileName.empty()) {
                if (!std::filesystem::exists(logDir)) {
                    std::filesystem::create_directories(logDir);
                }
                std::filesystem::path logFilePath = logDir / logFileName;

                // Rotate log file when it reaches 5MB, keep 3 rotated files
                auto file_sink = std::make_shared<spdlog::sinks::rotating_file_sink_mt>(logFilePath.string(), 1024 * 1024 * 5, 3);
                file_sink->set_level(fileLevel);
                file_sink->set_pattern("[%Y-%m-%d %H:%M:%S.%e] [%n] [%l] %v"); // Slightly different pattern for file
                sinks.push_back(file_sink);
            }

            // Create a logger with multiple sinks
            // For async logging (better performance for frequent logs, but requires spdlog::shutdown()):
            // spdlog::init_thread_pool(8192, 1); // queue size and 1 worker thread
            // s_CoreLogger = std::make_shared<spdlog::async_logger>("LauncherCore", sinks.begin(), sinks.end(), spdlog::thread_pool(), spdlog::async_overflow_policy::block);

            // For synchronous logging (simpler):
            s_CoreLogger = std::make_shared<spdlog::logger>("LauncherCore", sinks.begin(), sinks.end());
            
            spdlog::register_logger(s_CoreLogger); // Register the logger to make it accessible globally via spdlog::get if needed
            s_CoreLogger->set_level(spdlog::level::trace); // Log all messages, sinks will filter
            s_CoreLogger->flush_on(spdlog::level::trace);  // Flush immediately for all levels (good for debugging)

            CORE_LOG_INFO("Logger initialized. Console level: {}, File level: {}", spdlog::level::to_string_view(consoleLevel), spdlog::level::to_string_view(fileLevel));

        } catch (const spdlog::spdlog_ex& ex) {
            std::cerr << "Log initialization failed: " << ex.what() << std::endl;
            // Fallback: Create a simple console logger if init fails
            s_CoreLogger = spdlog::stdout_color_mt("LauncherCore_Fallback");
            s_CoreLogger->set_level(spdlog::level::err); // Only log errors
            s_CoreLogger->error("LOGGER INITIALIZATION FAILED. USING FALLBACK CONSOLE LOGGER.");
        } catch (const std::exception& ex) { // Catch filesystem errors too
             std::cerr << "Log file system setup failed: " << ex.what() << std::endl;
             s_CoreLogger = spdlog::stdout_color_mt("LauncherCore_FS_Fallback");
             s_CoreLogger->set_level(spdlog::level::err);
             s_CoreLogger->error("LOGGER FILE SYSTEM SETUP FAILED. USING FALLBACK CONSOLE LOGGER.");
        }
    }

    std::shared_ptr<spdlog::logger>& Logger::GetCoreLogger() {
        // Ensure logger is initialized, even if Init() wasn't called explicitly (basic fallback)
        if (!s_CoreLogger) {
            std::cerr << "Warning: Logger::GetCoreLogger() called before Logger::Init(). Initializing with default console logger." << std::endl;
            // Create a minimal console logger as a fallback.
            s_CoreLogger = spdlog::stdout_color_mt("LauncherCore_Default");
            s_CoreLogger->set_level(spdlog::level::warn); // Default to warning if not explicitly initialized
            s_CoreLogger->warn("Logger was not explicitly initialized. Using default settings.");
        }
        return s_CoreLogger;
    }

    // Implementation for GetOrCreate if you add it later
    // std::shared_ptr<spdlog::logger> Logger::GetOrCreate(const std::string& name) {
    //     // ...
    // }

} // namespace Utils
} // namespace Launcher