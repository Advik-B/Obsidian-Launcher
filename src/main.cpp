#include <iostream>
#include <iomanip>
#include <string>
#include <sstream>
#include <cpr/cpr.h>
#include <openssl/evp.h>
#include <cacert_pem.h>

// Function to calculate SHA-512 hash of a string
std::string sha512_string(const std::string& input) {
    EVP_MD_CTX* context = EVP_MD_CTX_new();
    EVP_DigestInit_ex(context, EVP_sha512(), nullptr);
    EVP_DigestUpdate(context, input.data(), input.size());

    unsigned char hash[EVP_MAX_MD_SIZE];
    unsigned int hash_length = 0;
    EVP_DigestFinal_ex(context, hash, &hash_length);
    EVP_MD_CTX_free(context);

    std::stringstream ss;
    for (unsigned int i = 0; i < hash_length; ++i) {
        ss << std::hex << std::setw(2) << std::setfill('0')
           << static_cast<int>(hash[i]);
    }
    return ss.str();
}

void print_usage() {
    std::cout << "Usage:\n"
              << "  cpr_ssl_test <url>\n";
}

int main(int argc, char* argv[]) {
    if (argc != 2) {
        print_usage();
        return 1;
    }

    const std::string url = argv[1];
    std::cout << "Downloading from URL: " << url << std::endl;

    // Prepare SSL options to point at your cacert.pem
    cpr::SslOptions sslOpts = cpr::Ssl(
        cpr::ssl::CaBuffer{cacert_pem}
    );

    // Create session
    cpr::Session session;
    session.SetUrl(cpr::Url{url});
    session.SetTimeout(cpr::Timeout{30000});
    session.SetSslOptions(sslOpts);

    // Perform GET
    cpr::Response response = session.Get();

    // Check status
    if (response.error || response.status_code != 200) {
        std::cerr << "\nError: HTTP failed ("
                  << response.status_code << ")\n";
        if (!response.error.message.empty()) {
            std::cerr << "  Details: " << response.error.message << "\n";
        }
        return 1;
    }

    std::cout << "\nDownload complete!\n"
              << "Response size: " << response.text.size() << " bytes\n";

    // Hash and print
    std::cout << "Calculating SHA-512 hash...\n";
    std::string hash = sha512_string(response.text);
    std::cout << "SHA-512: " << hash << "\n";

    // Optional: print headers
    std::cout << "\nResponse headers:\n";
    for (const auto& h : response.header) {
        std::cout << h.first << ": " << h.second << "\n";
    }

    return 0;
}
