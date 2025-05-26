// src/Utils/Crypto.cpp
#include <Launcher/Utils/Crypto.hpp>
#include <Launcher/Utils/Logger.hpp> // Ensure this is included

#include <openssl/sha.h>
#include <openssl/evp.h>
#include <fstream>
#include <vector>
#include <iomanip>
#include <sstream>

namespace Launcher::Utils {

    // Helper function to convert raw hash bytes to a hexadecimal string (no logging needed here)
    static std::string bytesToHexString(const unsigned char *bytes, size_t len) {
        std::ostringstream ss;
        ss << std::hex << std::setfill('0');
        for (size_t i = 0; i < len; ++i) {
            ss << std::setw(2) << static_cast<int>(bytes[i]);
        }
        return ss.str();
    }

    std::string calculateFileSHA1(const std::string &filePath) {
        // No class-specific logger here, use CORE_LOG or a generic "Crypto" named logger if preferred
        CORE_LOG_TRACE("[Crypto] Calculating SHA1 for file: {}", filePath);
        std::ifstream file(filePath, std::ios::binary);
        if (!file.is_open()) {
            CORE_LOG_ERROR("[Crypto] Could not open file for SHA1 calculation: {}", filePath);
            return "";
        }

        SHA_CTX sha1Context;
        if (!SHA1_Init(&sha1Context)) {
            CORE_LOG_ERROR("[Crypto] SHA1_Init failed for file: {}.", filePath);
            return "";
        }

        constexpr size_t bufferSize = 4096;
        std::vector<char> buffer(bufferSize);

        while (file.good()) {
            file.read(buffer.data(), bufferSize);
            std::streamsize bytesRead = file.gcount();
            if (bytesRead > 0) {
                if (!SHA1_Update(&sha1Context, buffer.data(), static_cast<size_t>(bytesRead))) {
                    CORE_LOG_ERROR("[Crypto] SHA1_Update failed for file: {}", filePath);
                    file.close();
                    return "";
                }
            }
        }
        file.close();

        unsigned char hash[SHA_DIGEST_LENGTH];
        if (!SHA1_Final(hash, &sha1Context)) {
            CORE_LOG_ERROR("[Crypto] SHA1_Final failed for file: {}", filePath);
            return "";
        }
        std::string hexHash = bytesToHexString(hash, SHA_DIGEST_LENGTH);
        CORE_LOG_TRACE("[Crypto] SHA1 for {}: {}", filePath, hexHash);
        return hexHash;
    }

    std::string calculateFileSHA256(const std::string &filePath) {
        CORE_LOG_TRACE("[Crypto] Calculating SHA256 for file: {}", filePath);
        std::ifstream file(filePath, std::ios::binary);
        if (!file.is_open()) {
            CORE_LOG_ERROR("[Crypto] Could not open file for SHA256 calculation: {}", filePath);
            return "";
        }

        EVP_MD_CTX *mdctx = EVP_MD_CTX_new();
        if (mdctx == nullptr) {
            CORE_LOG_ERROR("[Crypto] EVP_MD_CTX_new failed for SHA256 on file: {}", filePath);
            file.close(); // Close file before returning
            return "";
        }

        if (1 != EVP_DigestInit_ex(mdctx, EVP_sha256(), nullptr)) {
            CORE_LOG_ERROR("[Crypto] EVP_DigestInit_ex for SHA256 failed on file: {}", filePath);
            EVP_MD_CTX_free(mdctx);
            file.close();
            return "";
        }

        constexpr size_t bufferSize = 4096;
        std::vector<char> buffer(bufferSize);

        while (file.good()) {
            file.read(buffer.data(), bufferSize);
            std::streamsize bytesRead = file.gcount();
            if (bytesRead > 0) {
                if (1 != EVP_DigestUpdate(mdctx, buffer.data(), static_cast<size_t>(bytesRead))) {
                    CORE_LOG_ERROR("[Crypto] EVP_DigestUpdate failed for SHA256 on file: {}", filePath);
                    EVP_MD_CTX_free(mdctx);
                    file.close();
                    return "";
                }
            }
        }
        file.close();

        unsigned char hash[EVP_MAX_MD_SIZE];
        unsigned int hashLen = 0;

        if (1 != EVP_DigestFinal_ex(mdctx, hash, &hashLen)) {
            CORE_LOG_ERROR("[Crypto] EVP_DigestFinal_ex failed for SHA256 on file: {}", filePath);
            EVP_MD_CTX_free(mdctx);
            return "";
        }
        EVP_MD_CTX_free(mdctx);

        if (hashLen != SHA256_DIGEST_LENGTH) {
            CORE_LOG_WARN("[Crypto] SHA256 digest length is {}, expected {} for file: {}", hashLen,
                          SHA256_DIGEST_LENGTH, filePath);
        }

        std::string hexHash = bytesToHexString(hash, hashLen);
        CORE_LOG_TRACE("[Crypto] SHA256 for {}: {}", filePath, hexHash);
        return hexHash;
    }

} // namespace Launcher::Utils
