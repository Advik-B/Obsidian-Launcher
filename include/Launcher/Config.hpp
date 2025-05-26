// include/Launcher/Config.hpp
#ifndef LAUNCHER_CONFIG_HPP
#define LAUNCHER_CONFIG_HPP
#include <filesystem>
#include <iostream> // For create_directories logging

namespace Launcher {
    struct Config {
        std::filesystem::path baseDataPath;
        std::filesystem::path javaRuntimesDir;
        std::filesystem::path assetsDir;
        std::filesystem::path librariesDir;
        std::filesystem::path versionsDir;
        // Add other paths as needed (e.g., natives_directory)

        Config(const std::filesystem::path& base = "./.mylauncher_data") : baseDataPath(base) {
            javaRuntimesDir = baseDataPath / "java_runtimes";
            assetsDir = baseDataPath / "assets";
            librariesDir = baseDataPath / "libraries";
            versionsDir = baseDataPath / "versions";

            // Ensure directories exist
            auto create_dir_if_not_exists = [](const std::filesystem::path& p, const std::string& name){
                if (!std::filesystem::exists(p)) {
                    if (std::filesystem::create_directories(p)) {
                        std::cout << "Created " << name << " directory: " << p.string() << std::endl;
                    } else {
                        std::cerr << "Failed to create " << name << " directory: " << p.string() << std::endl;
                    }
                }
            };

            create_dir_if_not_exists(baseDataPath, "Base Data");
            create_dir_if_not_exists(javaRuntimesDir, "Java Runtimes");
            create_dir_if_not_exists(javaRuntimesDir / "_downloads" / "mojang", "Mojang Downloads"); // For JavaDownloader
            create_dir_if_not_exists(javaRuntimesDir / "_downloads" / "adoptium", "Adoptium Downloads"); // For JavaDownloader
            create_dir_if_not_exists(assetsDir, "Assets");
            create_dir_if_not_exists(assetsDir / "objects", "Asset Objects");
            create_dir_if_not_exists(assetsDir / "indexes", "Asset Indexes");
            create_dir_if_not_exists(librariesDir, "Libraries");
            create_dir_if_not_exists(versionsDir, "Versions");
        }
    };
}
#endif