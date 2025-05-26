// include/Launcher/Utils/Logger.hpp
#ifndef LOGGER_UTIL_HPP
#define LOGGER_UTIL_HPP

#include <spdlog/spdlog.h>
#include <spdlog/sinks/stdout_color_sinks.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <memory>
#include <vector>
#include <filesystem>
#include <string>
#include <map> // For storing named loggers

namespace Launcher::Utils {

    class Logger {
    public:
        // Call this once at the beginning of your application
        static void Init(const std::filesystem::path &logDir = "./logs",
                         const std::string &logFileName = "launcher.log",
                         spdlog::level::level_enum consoleLevel = spdlog::level::info,
                         spdlog::level::level_enum fileLevel = spdlog::level::trace);

        // Get the default core logger (e.g., for main.cpp or general messages)
        static std::shared_ptr<spdlog::logger> &GetCoreLogger();

        // Get or create a named logger
        // This will create the logger if it doesn't exist, using the sinks from Init()
        static std::shared_ptr<spdlog::logger> GetOrCreateLogger(const std::string &name);

        // Optional: set level for a specific logger
        static void SetLevel(const std::string &loggerName, spdlog::level::level_enum level);


    private:
        static std::vector<spdlog::sink_ptr> s_GlobalSinks; // Store initialized sinks
        static std::shared_ptr<spdlog::logger> s_CoreLogger;
        // Use spdlog's global registry for named loggers to avoid managing our own map explicitly here.
        // We just need to ensure they are created with the shared sinks.
    };

} // namespace Launcher::Utils

// Convenience macros using the Core Logger (can still be useful for general app logs)
#define CORE_LOG_TRACE(...)    if(auto& logger = ::Launcher::Utils::Logger::GetCoreLogger(); logger) { logger->trace(__VA_ARGS__); }
#define CORE_LOG_INFO(...)     if(auto& logger = ::Launcher::Utils::Logger::GetCoreLogger(); logger) { logger->info(__VA_ARGS__); }
#define CORE_LOG_WARN(...)     if(auto& logger = ::Launcher::Utils::Logger::GetCoreLogger(); logger) { logger->warn(__VA_ARGS__); }
#define CORE_LOG_ERROR(...)    if(auto& logger = ::Launcher::Utils::Logger::GetCoreLogger(); logger) { logger->error(__VA_ARGS__); }
#define CORE_LOG_CRITICAL(...) if(auto& logger = ::Launcher::Utils::Logger::GetCoreLogger(); logger) { logger->critical(__VA_ARGS__); }

// No specific macros for named loggers needed here, classes will hold their own logger instances.

#endif // LOGGER_UTIL_HPP