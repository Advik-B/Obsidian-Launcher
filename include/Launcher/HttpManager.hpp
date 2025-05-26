// include/Launcher/HttpManager.hpp
#ifndef HTTP_MANAGER_HPP
#define HTTP_MANAGER_HPP

#include <cpr/cpr.h> // For cpr types
#include <string>
#include <filesystem>
#include <optional>
#include <mutex>     // For thread safety if managing shared resources later
#include <spdlog/logger.h> // For logger

namespace Launcher {

    // Forward declare if not already (though cpr.h brings them)
    // class SslOptions;
    // class Url;
    // class Parameters;
    // class Header;
    // class Response;

    class HttpManager {
    public:
        HttpManager(); // Constructor can initialize default SSL options, etc.
        ~HttpManager();

        // Configuration methods (optional, could be set in constructor)
        // voidsetDefaultTimeout(cpr::Timeout timeout);
        // voidsetDefaultProxies(cpr::Proxies proxies);

        cpr::Response Get(const cpr::Url& url, const cpr::Parameters& parameters = {});
        cpr::Response Get(const cpr::Url& url, const cpr::Header& header, const cpr::Parameters& parameters = {});

        // Download to an already open ofstream
        cpr::Response Download(std::ofstream& sink, const cpr::Url& url);
        // Download to a specified filepath
        cpr::Response Download(const std::filesystem::path& filepath, const cpr::Url& url);

        // Potentially add Post, Put, Delete etc. later
        // cpr::Response Post(...);

        // If you want verbose mode for a specific operation temporarily
        cpr::Response GetVerbose(const cpr::Url& url, const cpr::Parameters& parameters = {});
        cpr::Response DownloadVerbose(const std::filesystem::path& filepath, const cpr::Url& url);


    private:
        cpr::SslOptions m_globalSslOptions;
        std::shared_ptr<spdlog::logger> m_logger;
        // std::mutex m_sessionMutex; // If you were to implement a session pool

        // Helper to create a new session with global defaults
        cpr::Session CreateSession() const;
    };

} // namespace Launcher

#endif // HTTP_MANAGER_HPP