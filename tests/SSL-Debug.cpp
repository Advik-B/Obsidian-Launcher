//
// Created by Advik on 26-05-2025.
//
// tests/ssl_debug.cpp
#include <Launcher/Config.hpp> // Your Config for logger base path
#include <Launcher/Http.hpp> // Your HTTP wrapper
#include <Launcher/Utils/Logger.hpp> // Your Logger
#include <cpr/cpr.h> // For cpr::Url, cpr::Session
#include <filesystem>
#include <iostream>
#include <spdlog/spdlog.h> // For spdlog::shutdown

#include "cacert_pem.h"

// Simple function to test a URL with verbose output
void test_url_verbose(const std::string& url_string, const std::string& test_name) {
    CORE_LOG_INFO("--- Testing URL ({}) ---", test_name);
    CORE_LOG_INFO("URL: {}", url_string);

    // We'll use a CPR Session directly to enable verbose mode for this test
    // and to explicitly use our Http namespace's SslOptions logic.

    // The SslOptions should be initialized when GetSslGlobalOptions() from Http.cpp is first called.
    // We can call it here to ensure it's initialized and to see its log messages.
    // Note: GetSslGlobalOptions is static in Http.cpp, so we can't call it directly.
    // Instead, we rely on our Http::Get or Http::Download to trigger its initialization.
    // For this isolated test, let's replicate the SslOptions setup or make it accessible.

    // To make SslOptions accessible for testing, we could:
    // 1. Expose InitializeSslOptions (make it public static in Http.hpp) - cleaner for testing
    // 2. Re-implement a similar SslOptions setup here for test purposes.

    // Let's assume for now Http::Get will correctly use the global options.
    // We will make a direct CPR call to enable verbose.

    cpr::Session session;
    session.SetUrl(cpr::Url{url_string});

    // Manually configure SSL options for this specific session for verbose testing
    // This replicates what GetSslGlobalOptions() in Http.cpp should be doing.
    cpr::SslOptions testSslOpts;
    if (cacert_pem != nullptr && strlen(cacert_pem) > 0) {
        CORE_LOG_INFO("[SSL_DEBUG] Using embedded CA cert buffer for test session.");
        testSslOpts = cpr::Ssl(
            cpr::ssl::CaBuffer{cacert_pem},
            cpr::ssl::VerifyHost{true},
            cpr::ssl::VerifyPeer{true},
            cpr::ssl::VerifyStatus{false}
        );
    } else {
        CORE_LOG_ERROR("[SSL_DEBUG] Embedded cacert_pem is null or empty for test session!");
        // Default CPR behavior without explicit CA
    }
    session.SetSslOptions(testSslOpts);
    session.SetVerbose(true); // Enable cURL's verbose output to stderr

    // Optional: Set a timeout
    session.SetTimeout(cpr::Timeout{10000}); // 10 seconds

    CORE_LOG_INFO("[SSL_DEBUG] Making GET request with verbose cURL output enabled...");
    cpr::Response r = session.Get();

    CORE_LOG_INFO("--- Test Results ({}) ---", test_name);
    CORE_LOG_INFO("URL: {}", r.url.str());
    CORE_LOG_INFO("Status code: {}", r.status_code);
    CORE_LOG_INFO("Error message: \"{}\"", r.error.message);
    CORE_LOG_INFO("CPR Error Code: {}", static_cast<int>(r.error.code));

    if (r.status_code == 0 && r.error.code != cpr::ErrorCode::OK) {
        CORE_LOG_ERROR("Request failed catastrophically (status 0, CPR error).");
    } else if (r.status_code >= 400) {
        CORE_LOG_WARN("Request returned HTTP error status.");
    } else if (r.status_code == 200) {
        CORE_LOG_INFO("Request successful (HTTP 200).");
    }

    CORE_LOG_INFO("Response text (first 200 chars): {:.200}", r.text);
    CORE_LOG_INFO("--------------------------\n");
}


int main() {
    Launcher::Config launcherConfig;
    std::filesystem::path logDir = launcherConfig.baseDataPath / "logs";
    Launcher::Utils::Logger::Init(logDir, "ssl_debug.log", spdlog::level::trace, spdlog::level::trace);

    CORE_LOG_INFO("Starting SSL Debug Test...");
    CORE_LOG_INFO("This program will attempt connections with verbose cURL output to help diagnose SSL issues.");
    CORE_LOG_INFO("Verbose cURL output will appear directly in the console (stderr).");

    // Ensure cacert_pem is linked and available
    // This will print its starting content if Http.cpp's InitializeSslOptions is called.
    // We can force an Http::Get to a known good, simple http (non-ssl) site if needed
    // to trigger initialization of the global SSL options if Http.cpp's static init hasn't run.
    // However, for this test, we explicitly set SslOptions on the session.

    if (cacert_pem == nullptr || strlen(cacert_pem) == 0) {
        CORE_LOG_CRITICAL("CRITICAL: cacert_pem string is NULL or empty! This is the primary suspect.");
    } else {
        CORE_LOG_INFO("cacert_pem string appears to be loaded. Length: {}", strlen(cacert_pem));
        CORE_LOG_TRACE("cacert_pem start: {:.100}", cacert_pem);
    }

    // Test with the problematic URLs
    test_url_verbose("https://api.adoptium.net/v3/assets/latest/17/hotspot?architecture=x64&heap_size=normal&image_type=jre&os=windows&vendor=eclipse", "Adoptium API");
    test_url_verbose("https://piston-meta.mojang.com/v1/packages/89ce85ccb518c62e18b4b58d63399ba2d9611426/manifest.json", "Mojang Java Manifest JSON");
    test_url_verbose("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json", "Mojang Version Manifest");
    test_url_verbose("https://google.com", "Google (standard HTTPS test)"); // Good baseline


    CORE_LOG_INFO("SSL Debug Test Finished.");
    spdlog::shutdown();
    return 0;
}