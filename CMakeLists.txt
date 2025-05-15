cmake_minimum_required(VERSION 4.0)
project(MinecraftLauncher)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

include(FetchContent)
FetchContent_Declare(
        cpr
        GIT_REPOSITORY https://github.com/libcpr/cpr.git
        GIT_TAG dd967cb48ea6bcbad9f1da5ada0db8ac0d532c06
)

FetchContent_MakeAvailable(cpr)
set(BUILD_LIBCURL_DOCS "Build the cURL documentation" OFF)
set(BUILD_LIBCURL_DOCS "Build the testing suite" OFF)

FetchContent_Declare(
        json
        GIT_REPOSITORY https://github.com/nlohmann/json.git
        GIT_TAG v3.12.0
)
FetchContent_MakeAvailable(json)

#FetchContent_Declare(backward
#        GIT_REPOSITORY https://github.com/bombela/backward-cpp
#        GIT_TAG master  # or a version tag, such as v1.6
#        SYSTEM          # optional, the Backward include directory will be treated as system directory
#)
#FetchContent_MakeAvailable(backward)

add_executable(MinecraftLauncher src/main.cpp src/VersionMeta.cpp src/AssetIndex.cpp src/JavaVersion.cpp src/Library.cpp src/MinecraftJAR.cpp src/OperatingSystem.cpp src/Rule.cpp src/Version.cpp src/VersionArguments.cpp src/VersionLogging.cpp)
target_include_directories(MinecraftLauncher PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/include)

# If you want time zone support (e.g., parse "%z"), enable this:
target_compile_definitions(MinecraftLauncher PRIVATE HAS_STRING_VIEW)

target_link_libraries(MinecraftLauncher PRIVATE cpr::cpr nlohmann_json::nlohmann_json)