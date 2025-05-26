// src/Http.cpp
#include <Launcher/Http.hpp>
#include <Launcher/Utils/Logger.hpp> // Added
#include <cacert_pem.h>
// #include <iostream> // Removed

namespace Launcher::Http {

    static cpr::SslOptions InitializeSslOptions() {
        CORE_LOG_INFO("[Http] Initializing SSL Options with embedded CA cert.");
        return cpr::Ssl(cpr::ssl::CaBuffer{cacert_pem});
    }

    static const cpr::SslOptions &GetSslGlobalOptions() {
        static cpr::SslOptions ssl_opts_instance = InitializeSslOptions();
        return ssl_opts_instance;
    }

    cpr::Response Get(const cpr::Url &url, const cpr::Parameters &parameters) {
        CORE_LOG_TRACE("[Http] GET: {}", url.str());
        return cpr::Get(url, parameters, GetSslGlobalOptions());
    }

    cpr::Response Get(const cpr::Url &url, const cpr::Header &header, const cpr::Parameters &parameters) {
        CORE_LOG_TRACE("[Http] GET with headers: {}", url.str());
        return cpr::Get(url, header, parameters, GetSslGlobalOptions());
    }

    cpr::Response Download(std::ofstream &sink, const cpr::Url &url) {
        CORE_LOG_TRACE("[Http] DOWNLOAD to stream: {}", url.str());
        return cpr::Download(sink, url, GetSslGlobalOptions());
    }

    cpr::Response Download(const std::filesystem::path &filepath, const cpr::Url &url) {
        CORE_LOG_INFO("[Http] DOWNLOAD to file: {} -> {}", url.str(), filepath.string());
        std::ofstream file_stream(filepath, std::ios::binary | std::ios::trunc);
        if (!file_stream) {
            cpr::Response r;
            r.error.message = "Failed to open file for download: " + filepath.string();
            r.status_code = 0;
            CORE_LOG_ERROR("[Http] {}", r.error.message);
            return r;
        }

        cpr::Response response = cpr::Download(file_stream, url, GetSslGlobalOptions());
        file_stream.close();

        if (response.error.code != cpr::ErrorCode::OK || response.status_code < 200 || response.status_code >= 300) {
            CORE_LOG_ERROR("[Http] Download failed for {}. Status: {}, Error: {}.", url.str(),
                           response.status_code, response.error.message);
            if (std::filesystem::exists(filepath)) {
                std::error_code ec;
                std::filesystem::remove(filepath, ec);
                if (ec) {
                    CORE_LOG_WARN("[Http] Failed to remove partially downloaded file {}: {}", filepath.string(),
                                  ec.message());
                } else {
                    CORE_LOG_INFO("[Http] Removed partially downloaded file: {}", filepath.string());
                }
            }
        } else {
            CORE_LOG_INFO("[Http] Download successful for {} to {}. Size: {}", url.str(), filepath.string(),
                          response.downloaded_bytes);
        }
        return response;
    }

} // namespace Launcher::Http
