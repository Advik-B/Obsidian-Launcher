// include/Launcher/Utils/Crypto.hpp
#ifndef CRYPTO_UTIL_HPP
#define CRYPTO_UTIL_HPP

#include <string>
#include <vector> // Not strictly needed for these declarations, but often useful in crypto contexts

namespace Launcher {
    namespace Utils {

        /**
         * @brief Calculates the SHA1 hash of a given file.
         * @param filePath The path to the file.
         * @return A hex-encoded string of the SHA1 hash. Returns an empty string on error (e.g., file not found, OpenSSL error).
         */
        std::string calculateFileSHA1(const std::string& filePath);

        /**
         * @brief Calculates the SHA256 hash of a given file.
         * @param filePath The path to the file.
         * @return A hex-encoded string of the SHA256 hash. Returns an empty string on error (e.g., file not found, OpenSSL error).
         */
        std::string calculateFileSHA256(const std::string& filePath);

    } // namespace Utils
} // namespace Launcher

#endif //CRYPTO_UTIL_HPP