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

# Fetch Howard Hinnant's date library
FetchContent_Declare(
        date
        GIT_REPOSITORY https://github.com/HowardHinnant/date.git
        GIT_TAG        origin/master
)

# Try to locate curl from CPR's build
get_target_property(CURL_LIB cpr::cpr INTERFACE_LINK_LIBRARIES)
get_target_property(CURL_INCLUDE_DIRS cpr::cpr INTERFACE_INCLUDE_DIRECTORIES)
# These might still point to CPR itself, so try to dig deeper if necessary
# Sometimes CPR uses CURL::libcurl if it fetches curl itself.
# Set CURL variables before date looks for it
set(CURL_LIBRARY "${CURL_LIB}" CACHE STRING "" FORCE)
set(CURL_INCLUDE_DIR "${CURL_INCLUDE_DIRS}" CACHE PATH "" FORCE)

set(BUILD_TZ_LIB ON)
# Set CURL_LIBRARY and CURL_INCLUDE_DIR from libcpr

FetchContent_MakeAvailable(date)

add_executable(MinecraftLauncher src/main.cpp src/VersionMeta.cpp)
target_include_directories(MinecraftLauncher PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/include)
target_include_directories(MinecraftLauncher PRIVATE ${date_SOURCE_DIR}/include)

# If you want time zone support (e.g., parse "%z"), enable this:
target_compile_definitions(MinecraftLauncher PRIVATE HAS_STRING_VIEW)

target_link_libraries(MinecraftLauncher PRIVATE cpr::cpr nlohmann_json::nlohmann_json)

# Link tz lib if you're using date::tz.h (timezone database)
target_link_libraries(MinecraftLauncher PRIVATE date-tz)