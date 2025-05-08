#include <iostream>
#include <cpr/cpr.h>
#include <nlohmann/json.hpp>
#include <Launcher/Types/VersionMeta.hpp>


using json = nlohmann::json;

int main() {
    const auto manifest = json::parse(cpr::Get(cpr::Url{"https://launchermeta.mojang.com/mc/game/version_manifest_v2.json"}).text);

    for (auto version : manifest["versions"]) {
        auto v = VersionMeta::from_json(version);
        std::cout << v.releaseTime << '\n';
    }
    return 0;
}