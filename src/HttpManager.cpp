// src/HttpManager.cpp
#include <Launcher/HttpManager.hpp>
#include <Launcher/Utils/Logger.hpp> // For GetOrCreateLogger
#include <cacert_pem.h>            // Your embedded CA cert
#include <cstring>                 // For strlen
#include <fstream>                 // For std::ofstream in Download

namespace Launcher {

HttpManager::HttpManager() {
    m_logger = Utils::Logger::GetOrCreateLogger("HttpManager");
    m_logger->info("HttpManager initializing...");

    // Initialize global SSL options once
    if (cacert_pem != nullptr && strlen(cacert_pem) > 0) {
        m_logger->info("Configuring global SslOptions with embedded CA cert buffer (length: {}).", strlen(cacert_pem));
        m_globalSslOptions = cpr::Ssl(
            cpr::ssl::CaBuffer{cacert_pem}
        );
    } else {
        m_logger->error("HttpManager: Embedded cacert_pem is null or empty! SSL/TLS will likely fail or use system CAs.");
        // m_globalSslOptions will be default initialized (usually means system CAs or failure if none)
    }
    m_logger->info("HttpManager initialized.");
}

HttpManager::~HttpManager() {
    m_logger->info("HttpManager shutting down.");
}

// Helper to create and configure a new session for each request.
// This is the simplest way to ensure thread safety as each thread gets its own session.
cpr::Session HttpManager::CreateSession() const {
    cpr::Session session;
    session.SetSslOptions(m_globalSslOptions);
    // Set other defaults if you have them (e.g., timeout, default user-agent)
    // session.SetUserAgent("MyLauncher/1.0");
    return session;
}

cpr::Response HttpManager::Get(const cpr::Url& url, const cpr::Parameters& parameters) {
    m_logger->trace("GET: {}", url.str());
    cpr::Session session = CreateSession(); // New session per request
    session.SetUrl(url);
    session.SetParameters(parameters);
    return session.Get();
}

cpr::Response HttpManager::Get(const cpr::Url& url, const cpr::Header& header, const cpr::Parameters& parameters) {
    m_logger->trace("GET with headers: {}", url.str());
    cpr::Session session = CreateSession(); // New session per request
    session.SetUrl(url);
    session.SetHeader(header);
    session.SetParameters(parameters);
    return session.Get();
}

cpr::Response HttpManager::Download(std::ofstream& sink, const cpr::Url& url) {
    m_logger->trace("DOWNLOAD to provided ofstream: {}", url.str());
    cpr::Session session = CreateSession(); // New session per request
    session.SetUrl(url);

    cpr::Response response = session.Download(sink); // Call Download on the session

    // Log results (simplified, add more detail as needed)
    if(response.error.code != cpr::ErrorCode::OK || response.status_code < 200 || response.status_code >= 300) {
        m_logger->error("Download to stream failed for {}. Status: {}, Error: \"{}\", CPR Error Code: {}",
            url.str(), response.status_code, response.error.message, static_cast<int>(response.error.code));
    } else {
        m_logger->info("Download to stream successful for {}. Bytes: {}", url.str(), response.downloaded_bytes);
    }
    return response;
}

cpr::Response HttpManager::Download(const std::filesystem::path& filepath, const cpr::Url& url) {
    m_logger->info("DOWNLOAD to file: {} -> {}", url.str(), filepath.string());
    std::ofstream file_stream(filepath, std::ios::binary | std::ios::trunc);
    if (!file_stream) {
        cpr::Response r_fail;
        r_fail.error.code = cpr::ErrorCode::UNKNOWN_ERROR; // Or a custom internal error code
        r_fail.error.message = "HttpManager::Download: Failed to open file for writing: " + filepath.string();
        r_fail.status_code = 0;
        m_logger->error("{}", r_fail.error.message);
        return r_fail;
    }

    cpr::Session session = CreateSession(); // New session per request
    session.SetUrl(url);

    cpr::Response response = session.Download(file_stream); // Call Download on the session
    file_stream.close();

    if(response.error.code != cpr::ErrorCode::OK || response.status_code < 200 || response.status_code >= 300) {
        m_logger->error("Download to file failed for {}. Status: {}, Error: \"{}\", CPR Error Code: {}",
            url.str(), response.status_code, response.error.message, static_cast<int>(response.error.code));
        if (std::filesystem::exists(filepath)) {
            std::error_code ec;
            std::filesystem::remove(filepath, ec);
            if (ec) {
                m_logger->warn("Failed to remove partially downloaded file {}: {}", filepath.string(), ec.message());
            } else {
                m_logger->info("Removed partially downloaded file: {}", filepath.string());
            }
        }
    } else {
        m_logger->info("Download to file successful for {} to {}. Size: {}", url.str(), filepath.string(), response.downloaded_bytes);
    }
    return response;
}

// --- Verbose methods for debugging ---
cpr::Response HttpManager::GetVerbose(const cpr::Url& url, const cpr::Parameters& parameters) {
    m_logger->trace("VERBOSE GET: {}", url.str());
    cpr::Session session = CreateSession();
    session.SetUrl(url);
    session.SetParameters(parameters);
    session.SetVerbose(true); // Enable verbose for this session
    return session.Get();
}

cpr::Response HttpManager::DownloadVerbose(const std::filesystem::path& filepath, const cpr::Url& url) {
    m_logger->info("VERBOSE DOWNLOAD to file: {} -> {}", url.str(), filepath.string());
    std::ofstream file_stream(filepath, std::ios::binary | std::ios::trunc);
    if (!file_stream) {
        cpr::Response r_fail; // Simplified for brevity
        r_fail.error.code = cpr::ErrorCode::UNKNOWN_ERROR;
        r_fail.error.message = "HttpManager::DownloadVerbose: Failed to open file: " + filepath.string();
        m_logger->error(r_fail.error.message);
        return r_fail;
    }

    cpr::Session session = CreateSession();
    session.SetUrl(url);
    session.SetVerbose(true); // Enable verbose for this session

    cpr::Response response = session.Download(file_stream);
    file_stream.close();
    return response;
}


} // namespace Launcher