// tests/ssl_debug.cpp
// #include <Launcher/Http.hpp> // Old
#include <Launcher/HttpManager.hpp>  // New
#include <Launcher/Utils/Logger.hpp>
#include <Launcher/Config.hpp>
#include <spdlog/spdlog.h>
#include <cpr/cpr.h>
#include <iostream>
#include <filesystem>
#include <cacert_pem.h> // For direct check if needed

void test_url_verbose(Launcher::HttpManager& httpManager, const std::string& url_string, const std::string& test_name) {
    CORE_LOG_INFO("--- Testing URL ({}) ---", test_name);
    CORE_LOG_INFO("URL: {}", url_string);

    CORE_LOG_INFO("[SSL_DEBUG] Making GET request via HttpManager (verbose enabled for this call)...");
    // Use the verbose method from HttpManager
    cpr::Response r = httpManager.GetVerbose(cpr::Url{url_string});


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

    CORE_LOG_INFO("Starting SSL Debug Test (using HttpManager)...");

    if (cacert_pem == nullptr || strlen(cacert_pem) == 0) {
        CORE_LOG_CRITICAL("CRITICAL: cacert_pem string is NULL or empty! This is the primary suspect.");
    } else {
        CORE_LOG_INFO("cacert_pem string appears to be loaded. Length: {}", strlen(cacert_pem));
    }

    Launcher::HttpManager httpManager; // Create HttpManager instance for the test

    test_url_verbose(httpManager, "https://api.adoptium.net/v3/assets/latest/17/hotspot?architecture=x64&heap_size=normal&image_type=jre&os=windows&vendor=eclipse", "Adoptium API");
    test_url_verbose(httpManager, "https://piston-meta.mojang.com/v1/packages/89ce85ccb518c62e18b4b58d63399ba2d9611426/manifest.json", "Mojang Java Manifest JSON");
    test_url_verbose(httpManager, "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json", "Mojang Version Manifest");
    test_url_verbose(httpManager, "https://google.com", "Google (standard HTTPS test)");


    CORE_LOG_INFO("SSL Debug Test Finished.");
    spdlog::shutdown();
    return 0;
}