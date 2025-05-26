#ifndef LOGGER_UTIL_HPP
#define LOGGER_UTIL_HPP

// Must be before spdlog.h if you define SPDLOG_ACTIVE_LEVEL (for compile-time level filtering)
// #define SPDLOG_ACTIVE_LEVEL SPDLOG_LEVEL_TRACE // Example: Log all levels

#include <spdlog/spdlog.h>
#include <spdlog/sinks/stdout_color_sinks.h> // For console output
#include <spdlog/sinks/basic_file_sink.h>    // For file output
#include <spdlog/sinks/rotating_file_sink.h> // For rotating file logs
#include <memory>
#include <vector>
#include <filesystem>
#include <string> // For logger name


namespace Launcher::Utils {

    class Logger {
    public:
        // Call this once at the beginning of your application
        static void Init(const std::filesystem::path& logDir = "./logs",
                         const std::string& logFileName = "launcher.log",
                         spdlog::level::level_enum consoleLevel = spdlog::level::info,
                         spdlog::level::level_enum fileLevel = spdlog::level::trace);

        // Get the default core logger
        static std::shared_ptr<spdlog::logger>& GetCoreLogger();

        // Optional: Get or create a named logger
        // static std::shared_ptr<spdlog::logger> GetOrCreate(const std::string& name);

    private:
        static std::shared_ptr<spdlog::logger> s_CoreLogger;
        // static std::map<std::string, std::shared_ptr<spdlog::logger>> s_NamedLoggers;
    };

} // namespace Launcher::Utils


// Convenience macros using the Core Logger
#define CORE_LOG_TRACE(...)    if(::Launcher::Utils::Logger::GetCoreLogger()) { ::Launcher::Utils::Logger::GetCoreLogger()->trace(__VA_ARGS__); }
#define CORE_LOG_INFO(...)     if(::Launcher::Utils::Logger::GetCoreLogger()) { ::Launcher::Utils::Logger::GetCoreLogger()->info(__VA_ARGS__); }
#define CORE_LOG_WARN(...)     if(::Launcher::Utils::Logger::GetCoreLogger()) { ::Launcher::Utils::Logger::GetCoreLogger()->warn(__VA_ARGS__); }
#define CORE_LOG_ERROR(...)    if(::Launcher::Utils::Logger::GetCoreLogger()) { ::Launcher::Utils::Logger::GetCoreLogger()->error(__VA_ARGS__); }
#define CORE_LOG_CRITICAL(...) if(::Launcher::Utils::Logger::GetCoreLogger()) { ::Launcher::Utils::Logger::GetCoreLogger()->critical(__VA_ARGS__); }

// If you use named loggers, you might have macros like:
// #define MY_MODULE_LOG_INFO(logger_name, ...) if(auto l = ::Launcher::Utils::Logger::GetOrCreate(logger_name)) { l->info(__VA_ARGS__); }

#endif // LOGGER_UTIL_HPP