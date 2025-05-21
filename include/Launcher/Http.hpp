// include/Launcher/Http.hpp
#ifndef HTTP_WRAPPER_HPP
#define HTTP_WRAPPER_HPP

#include <cpr/cpr.h>
#include <string>
#include <filesystem> // For download path

namespace Launcher {
    namespace Http {

        // Wrapper for cpr::Get
        cpr::Response Get(const cpr::Url& url, const cpr::Parameters& parameters = {});
        cpr::Response Get(const cpr::Url& url, const cpr::Header& header, const cpr::Parameters& parameters = {});


        // Wrapper for cpr::Download
        cpr::Response Download(std::ofstream& sink, const cpr::Url& url);
        cpr::Response Download(const std::filesystem::path& filepath, const cpr::Url& url);


        // Add wrappers for cpr::Post, etc., as needed

    } // namespace Http
} // namespace Launcher

#endif //HTTP_WRAPPER_HPP