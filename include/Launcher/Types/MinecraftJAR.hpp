//
// Created by Advik on 09-05-2025.
//

#ifndef MINECRAFTJAR_HPP
#define MINECRAFTJAR_HPP
#include <string>

enum class MinecraftJARType {
    CLIENT = 1,
    SERVER = 2
};

struct MinecraftJAR {
    const MinecraftJARType type;
    const std::string sha1;
    const unsigned int size;
    const std::string url;
};

#endif //MINECRAFTJAR_HPP
