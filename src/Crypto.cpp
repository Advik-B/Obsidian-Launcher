// src/Utils/Crypto.cpp
#include <Launcher/Utils/Crypto.hpp>

#include <openssl/sha.h>   // For SHA1_... functions
#include <openssl/evp.h>   // For EVP_MD_CTX, EVP_sha256, etc. (modern interface)
#include <fstream>
#include <vector>
#include <iomanip>     // For std::setw, std::setfill
#include <sstream>     // For std::ostringstream
#include <iostream>    // For std::cerr

namespace Launcher {
namespace Utils {

// Helper function to convert raw hash bytes to a hexadecimal string
static std::string bytesToHexString(const unsigned char* bytes, size_t len) {
    std::ostringstream ss;
    ss << std::hex << std::setfill('0');
    for (size_t i = 0; i < len; ++i) {
        ss << std::setw(2) << static_cast<int>(bytes[i]);
    }
    return ss.str();
}

std::string calculateFileSHA1(const std::string& filePath) {
    std::ifstream file(filePath, std::ios::binary);
    if (!file.is_open()) {
        std::cerr << "Crypto Error: Could not open file for SHA1 calculation: " << filePath << std::endl;
        return "";
    }

    SHA_CTX sha1Context;
    if (!SHA1_Init(&sha1Context)) {
        std::cerr << "Crypto Error: SHA1_Init failed." << std::endl;
        // Note: OpenSSL errors can be retrieved using ERR_print_errors_fp(stderr); for more details
        return "";
    }

    constexpr size_t bufferSize = 4096;
    std::vector<char> buffer(bufferSize); // Using std::vector for safer buffer management

    while (file.good()) {
        file.read(buffer.data(), bufferSize);
        std::streamsize bytesRead = file.gcount();
        if (bytesRead > 0) {
            if (!SHA1_Update(&sha1Context, buffer.data(), static_cast<size_t>(bytesRead))) {
                std::cerr << "Crypto Error: SHA1_Update failed." << std::endl;
                return "";
            }
        }
    }
    // No need to check file.eof() or file.fail() explicitly here,
    // gcount() handles the last partial read correctly.
    file.close();

    unsigned char hash[SHA_DIGEST_LENGTH];
    if (!SHA1_Final(hash, &sha1Context)) {
        std::cerr << "Crypto Error: SHA1_Final failed." << std::endl;
        return "";
    }

    return bytesToHexString(hash, SHA_DIGEST_LENGTH);
}

std::string calculateFileSHA256(const std::string& filePath) {
    std::ifstream file(filePath, std::ios::binary);
    if (!file.is_open()) {
        std::cerr << "Crypto Error: Could not open file for SHA256 calculation: " << filePath << std::endl;
        return "";
    }

    // EVP (Envelope) interface is generally preferred for new code in OpenSSL
    // as it's more flexible and supports a wider range of algorithms.
    EVP_MD_CTX *mdctx = EVP_MD_CTX_new();
    if (mdctx == nullptr) {
        std::cerr << "Crypto Error: EVP_MD_CTX_new failed." << std::endl;
        return "";
    }

    // Initialize the digest context for SHA256
    if (1 != EVP_DigestInit_ex(mdctx, EVP_sha256(), nullptr)) {
        std::cerr << "Crypto Error: EVP_DigestInit_ex for SHA256 failed." << std::endl;
        EVP_MD_CTX_free(mdctx); // Clean up context
        return "";
    }

    constexpr size_t bufferSize = 4096;
    std::vector<char> buffer(bufferSize);

    while (file.good()) {
        file.read(buffer.data(), bufferSize);
        std::streamsize bytesRead = file.gcount();
        if (bytesRead > 0) {
            if (1 != EVP_DigestUpdate(mdctx, buffer.data(), static_cast<size_t>(bytesRead))) {
                std::cerr << "Crypto Error: EVP_DigestUpdate failed." << std::endl;
                EVP_MD_CTX_free(mdctx);
                return "";
            }
        }
    }
    file.close();

    unsigned char hash[EVP_MAX_MD_SIZE]; // EVP_MAX_MD_SIZE is sufficiently large for all EVP digests
    unsigned int hashLen = 0; // Will be filled by EVP_DigestFinal_ex

    if (1 != EVP_DigestFinal_ex(mdctx, hash, &hashLen)) {
        std::cerr << "Crypto Error: EVP_DigestFinal_ex failed." << std::endl;
        EVP_MD_CTX_free(mdctx);
        return "";
    }
    EVP_MD_CTX_free(mdctx); // Clean up the context

    // Ensure hashLen is within expected bounds for SHA256 if needed, though EVP_MAX_MD_SIZE handles it.
    // For SHA256, hashLen should be SHA256_DIGEST_LENGTH (32 bytes).
    if (hashLen != SHA256_DIGEST_LENGTH) {
         // This case should ideally not happen if EVP_sha256() was used correctly.
        std::cerr << "Crypto Warning: SHA256 digest length is " << hashLen << ", expected " << SHA256_DIGEST_LENGTH << std::endl;
    }


    return bytesToHexString(hash, hashLen);
}

} // namespace Utils
} // namespace Launcher