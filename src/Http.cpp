// src/Http.cpp
#include <Launcher/Http.hpp>
#include <Launcher/Utils/Logger.hpp>
#include <cacert_pem.h>
#include <cstring>      // For strlen

// We don't need <cpr/session.h> if we are only using global functions with options
// #include <cpr/session.h>

namespace Launcher {
namespace Http {

static std::shared_ptr<spdlog::logger>& get_http_logger() {
    static std::shared_ptr<spdlog::logger> http_logger = Utils::Logger::GetOrCreateLogger("Http");
    return http_logger;
}

cpr::SslOptions GetConfiguredSslOptions() {
    get_http_logger()->debug("Creating SslOptions for request.");
    cpr::SslOptions sslOpts;

    if (cacert_pem != nullptr && strlen(cacert_pem) > 0) {
        get_http_logger()->info("Configuring SslOptions with embedded CA cert buffer (length: {}).", strlen(cacert_pem));
        sslOpts = cpr::Ssl(
            cpr::ssl::CaBuffer{cacert_pem},
            cpr::ssl::VerifyHost{true},
            cpr::ssl::VerifyPeer{true},
            cpr::ssl::VerifyStatus{false}
        );
    } else {
        get_http_logger()->error("Embedded cacert_pem is null or empty in GetConfiguredSslOptions! SSL/TLS will likely fail or use system CAs.");
    }
    return sslOpts;
}

cpr::Response Get(const cpr::Url& url, const cpr::Parameters& parameters) {
    get_http_logger()->trace("GET: {}", url.str());
    return cpr::Get(url, parameters, GetConfiguredSslOptions());
}

cpr::Response Get(const cpr::Url& url, const cpr::Header& header, const cpr::Parameters& parameters) {
    get_http_logger()->trace("GET with headers: {}", url.str());
    return cpr::Get(url, header, parameters, GetConfiguredSslOptions());
}

cpr::Response Download(std::ofstream& sink, const cpr::Url& url) {
    get_http_logger()->trace("DOWNLOAD to provided ofstream: {}", url.str());

    cpr::Response response = cpr::Download(sink, url, GetConfiguredSslOptions());

    if(response.error.code != cpr::ErrorCode::OK || response.status_code < 200 || response.status_code >= 300) {
        get_http_logger()->error("Download to stream failed for {}. Status: {}, Error: \"{}\"",
            url.str(),
            response.status_code,
            response.error.message
        );
    } else {
        get_http_logger()->info("Download to stream successful for {}. Bytes: {}", url.str(), response.downloaded_bytes);
    }
    return response;
}

cpr::Response Download(const std::filesystem::path& filepath, const cpr::Url& url) {
    get_http_logger()->info("DOWNLOAD to file: {} -> {}", url.str(), filepath.string());
    std::ofstream file_stream(filepath, std::ios::binary | std::ios::trunc);
    if (!file_stream) {
        cpr::Response r;
        // For ofstream failure, this is an application-level I/O error, not a CPR error yet.
        // We can set a generic CPR error or a custom one if CPR doesn't have a perfect fit.
        // Let's use UNKNOWN_ERROR if a specific IO_ERROR isn't available or suitable.
        r.error.code = cpr::ErrorCode::UNKNOWN_ERROR; // Or handle as a non-CPR error
        r.error.message = "Http::Download: Failed to open file for writing: " + filepath.string();
        r.status_code = 0; // Indicate application-level failure before HTTP attempt
        get_http_logger()->error("{}", r.error.message);
        return r;
    }

    cpr::Response response = cpr::Download(file_stream, url, GetConfiguredSslOptions());
    file_stream.close();

    if(response.error.code != cpr::ErrorCode::OK || response.status_code < 200 || response.status_code >= 300) {
        // The error message from CPR itself is usually more informative for network/SSL issues.
        get_http_logger()->error("Download to file failed for {}. Status: {}, Error: \"{}\"",
            url.str(),
            response.status_code,
            response.error.message
        );
        if (std::filesystem::exists(filepath)) {
            std::error_code ec;
            std::filesystem::remove(filepath, ec);
            if (ec) {
                get_http_logger()->warn("Failed to remove partially downloaded file {}: {}", filepath.string(), ec.message());
            } else {
                get_http_logger()->info("Removed partially downloaded file: {}", filepath.string());
            }
        }
    } else {
        get_http_logger()->info("Download to file successful for {} to {}. Size: {}", url.str(), filepath.string(), response.downloaded_bytes);
    }
    return response;
}

} // namespace Http
} // namespace Launcher