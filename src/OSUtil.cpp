// src/Utils/OS.cpp
#include <Launcher/Utils/OS.hpp>

namespace Launcher {
namespace Utils {

OperatingSystem getCurrentOS() {
    #if defined(_WIN32) || defined(_WIN64)
        return OperatingSystem::WINDOWS;
    #elif defined(__APPLE__) || defined(__MACH__)
        return OperatingSystem::MACOS;
    #elif defined(__linux__)
        return OperatingSystem::LINUX;
    #else
        return OperatingSystem::UNKNOWN;
    #endif
}

Architecture getCurrentArch() {
    #if defined(_M_AMD64) || defined(__amd64__) || defined(__x86_64__)
        return Architecture::X64;
    #elif defined(_M_IX86) || defined(__i386__)
        return Architecture::X86;
    #elif defined(__aarch64__)
        return Architecture::ARM64;
    #elif defined(__arm__)
        return Architecture::ARM32;
    #else
        return Architecture::UNKNOWN;
    #endif
}

// For Mojang's manifest
std::string getOSStringForJavaManifest(OperatingSystem os, Architecture arch) {
    switch (os) {
        case OperatingSystem::WINDOWS:
            if (arch == Architecture::X64) return "windows-x64";
            if (arch == Architecture::X86) return "windows-x86";
            if (arch == Architecture::ARM64) return "windows-arm64";
            break;
        case OperatingSystem::MACOS:
            if (arch == Architecture::X64) return "mac-os";
            if (arch == Architecture::ARM64) return "mac-os-arm64";
            break;
        case OperatingSystem::LINUX:
             if (arch == Architecture::X64) return "linux";
             if (arch == Architecture::ARM64) return "linux-aarch64";
             if (arch == Architecture::ARM32) return "linux-arm"; // Assuming Adoptium uses this
            break;
        default:
            break;
    }
    return "unknown-os-arch-mojang";
}

// For Adoptium API
std::string getOSStringForAdoptium(OperatingSystem os) {
    switch (os) {
        case OperatingSystem::WINDOWS: return "windows";
        case OperatingSystem::MACOS: return "mac";
        case OperatingSystem::LINUX: return "linux";
        default: return "";
    }
}

std::string getArchStringForAdoptium(Architecture arch) {
    switch (arch) {
        case Architecture::X64: return "x64";
        case Architecture::X86: return "x86"; // Adoptium uses x86 for 32-bit Windows
        case Architecture::ARM64: return "aarch64";
        case Architecture::ARM32: return "arm"; // Adoptium uses 'arm' for 32-bit ARM
        default: return "";
    }
}


} // namespace Utils
} // namespace Launcher