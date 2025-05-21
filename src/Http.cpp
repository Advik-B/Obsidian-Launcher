// src/Http.cpp
#include <Launcher/Http.hpp>
#include <cacert_pem.h>
#include <iostream> // For potential error logging

namespace Launcher {
namespace Http {


static cpr::SslOptions sslOpts = cpr::Ssl(
    cpr::ssl::CaBuffer{cacert_pem}
);

cpr::Response Get(const cpr::Url& url, const cpr::Parameters& parameters) {
    return cpr::Get(url, parameters, sslOpts);
}

cpr::Response Get(const cpr::Url& url, const cpr::Header& header, const cpr::Parameters& parameters) {
    return cpr::Get(url, header, parameters, sslOpts);
}


cpr::Response Download(std::ofstream& sink, const cpr::Url& url) {
    return cpr::Download(sink, url, sslOpts);
}

cpr::Response Download(const std::filesystem::path& filepath, const cpr::Url& url) {
    std::ofstream file_stream(filepath, std::ios::binary);
    if (!file_stream) {
        cpr::Response r;
        r.error.code = cpr::ErrorCode::UNKNOWN_ERROR; // Or a custom error
        r.error.message = "Failed to open file for download: " + filepath.string();
        r.status_code = 0; // Indicate failure
        std::cerr << r.error.message << std::endl;
        return r;
    }

    return cpr::Download(file_stream, url, sslOpts);
}


} // namespace Http
} // namespace Launcher