// src/Utils/Logger.cpp
#include <Launcher/Utils/Logger.hpp>
#include <iostream> // For initial error before logger is set up

namespace Launcher {
namespace Utils {

    std::shared_ptr<spdlog::logger> Logger::s_CoreLogger;
    std::vector<spdlog::sink_ptr> Logger::s_GlobalSinks; // Definition

    void Logger::Init(const std::filesystem::path& logDir,
                      const std::string& logFileName,
                      spdlog::level::level_enum consoleLevel,
                      spdlog::level::level_enum fileLevel) {
        try {
            s_GlobalSinks.clear(); // Clear previous sinks if re-initializing (though Init should be once)

            // Console Sink (thread-safe)
            auto console_sink = std::make_shared<spdlog::sinks::stdout_color_sink_mt>();
            console_sink->set_level(consoleLevel);
            console_sink->set_pattern("%^[%Y-%m-%d %H:%M:%S.%e] [%n] [%l] %v%$"); // %n is logger name
            s_GlobalSinks.push_back(console_sink);

            if (!logDir.empty() && !logFileName.empty()) {
                if (!std::filesystem::exists(logDir)) {
                    std::filesystem::create_directories(logDir);
                }
                std::filesystem::path logFilePath = logDir / logFileName;
                auto file_sink = std::make_shared<spdlog::sinks::rotating_file_sink_mt>(logFilePath.string(), 1024 * 1024 * 5, 3);
                file_sink->set_level(fileLevel);
                file_sink->set_pattern("[%Y-%m-%d %H:%M:%S.%e] [%n] [%l] %v"); // %n is logger name
                s_GlobalSinks.push_back(file_sink);
            }

            // Create and register the core logger
            s_CoreLogger = std::make_shared<spdlog::logger>("Core", s_GlobalSinks.begin(), s_GlobalSinks.end());
            spdlog::register_logger(s_CoreLogger);
            s_CoreLogger->set_level(spdlog::level::trace);
            s_CoreLogger->flush_on(spdlog::level::trace);

            // No need for spdlog::init_thread_pool for synchronous loggers
            // For async:
            // spdlog::init_thread_pool(8192, 1);
            // s_CoreLogger = spdlog::create_async_nb<spdlog::sinks::null_sink_mt>("Core"); // Or with sinks
            // spdlog::register_logger(s_CoreLogger);
            // s_CoreLogger->set_sinks(s_GlobalSinks); // Then set sinks if created with null_sink

            s_CoreLogger->info("Logger initialized. Console level: {}, File level: {}",
                               spdlog::level::to_string_view(consoleLevel),
                               spdlog::level::to_string_view(fileLevel));

        } catch (const spdlog::spdlog_ex& ex) {
            std::cerr << "Log initialization failed: " << ex.what() << std::endl;
            s_CoreLogger = spdlog::stdout_color_mt("Core_Fallback");
            s_CoreLogger->set_level(spdlog::level::err);
            s_CoreLogger->error("LOGGER INITIALIZATION FAILED. USING FALLBACK CONSOLE LOGGER.");
        } catch (const std::exception& ex) {
            std::cerr << "Log file system setup failed: " << ex.what() << std::endl;
            s_CoreLogger = spdlog::stdout_color_mt("Core_FS_Fallback");
            s_CoreLogger->set_level(spdlog::level::err);
            s_CoreLogger->error("LOGGER FILE SYSTEM SETUP FAILED. USING FALLBACK CONSOLE LOGGER.");
        }
    }

    std::shared_ptr<spdlog::logger>& Logger::GetCoreLogger() {
        if (!s_CoreLogger) {
            std::cerr << "Warning: Logger::GetCoreLogger() called before Logger::Init(). Initializing with default console logger." << std::endl;
            Init("", "", spdlog::level::warn, spdlog::level::trace); // Basic fallback
        }
        return s_CoreLogger;
    }

    std::shared_ptr<spdlog::logger> Logger::GetOrCreateLogger(const std::string& name) {
        auto logger = spdlog::get(name); // Check if already exists
        if (!logger) {
            if (s_GlobalSinks.empty()) {
                // This case should ideally not happen if Init() was called.
                // If it does, the named logger will only have a default console sink.
                std::cerr << "Warning: Logger::GetOrCreateLogger(" << name << ") called before Logger::Init() or Init failed to create sinks. Creating logger with default console sink." << std::endl;
                logger = spdlog::stdout_color_mt(name);
            } else {
                // Create a new logger using the globally initialized sinks
                logger = std::make_shared<spdlog::logger>(name, s_GlobalSinks.begin(), s_GlobalSinks.end());
            }
            logger->set_level(spdlog::level::trace); // Default to trace, sinks will filter
            logger->flush_on(spdlog::level::trace);
            spdlog::register_logger(logger);
        }
        return logger;
    }

    void Logger::SetLevel(const std::string& loggerName, spdlog::level::level_enum level) {
        auto logger = spdlog::get(loggerName);
        if (logger) {
            logger->set_level(level);
        } else {
            if (s_CoreLogger) { // Log to core logger if available
                 s_CoreLogger->warn("Attempted to set level for non-existent logger: {}", loggerName);
            } else {
                 std::cerr << "Attempted to set level for non-existent logger: " << loggerName << " (Core logger also unavailable)" << std::endl;
            }
        }
    }

} // namespace Utils
} // namespace Launcher