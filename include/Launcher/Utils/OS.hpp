// include/Launcher/Utils/OS.hpp
#ifndef OS_UTIL_HPP
#define OS_UTIL_HPP

#include <string>

namespace Launcher {
    namespace Utils {

        enum class OperatingSystem {
            WINDOWS,
            MACOS,
            LINUX,
            UNKNOWN
        };

        enum class Architecture {
            X86,        // 32-bit x86
            X64,        // 64-bit x86_64/amd64
            ARM64,      // 64-bit ARM (aarch64)
            ARM32,      // 32-bit ARM
            UNKNOWN
        };

        OperatingSystem getCurrentOS();
        Architecture getCurrentArch();

        // For Mojang's Java Manifest
        std::string getOSStringForJavaManifest(OperatingSystem os, Architecture arch);

        // For Adoptium API
        std::string getOSStringForAdoptium(OperatingSystem os);
        std::string getArchStringForAdoptium(Architecture arch);


    } // namespace Utils
} // namespace Launcher

#endif //OS_UTIL_HPP