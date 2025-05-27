cmake_minimum_required(VERSION 3.16)
project(ObsidianLauncher)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

include(FetchContent)

# Platform-specific OpenSSL hints
if(WIN32)
    list(APPEND CMAKE_PREFIX_PATH "C:/Programs/OpenSSL-Win64")
elseif(APPLE)
    list(APPEND CMAKE_PREFIX_PATH "/usr/local/opt/openssl" "/opt/homebrew/opt/openssl")
endif()

# Find OpenSSL
find_package(OpenSSL REQUIRED)
message(STATUS "OpenSSL found: ${OPENSSL_FOUND}")
message(STATUS "OpenSSL version: ${OPENSSL_VERSION}")
message(STATUS "OpenSSL include dir: ${OPENSSL_INCLUDE_DIR}")
message(STATUS "OpenSSL libraries: ${OPENSSL_LIBRARIES}")


set(CPR_ENABLE_SSL ON)

FetchContent_Declare(
        cpr
        GIT_REPOSITORY https://github.com/libcpr/cpr.git
        GIT_TAG 1.10.5
)
FetchContent_MakeAvailable(cpr)

FetchContent_Declare(
        json
        GIT_REPOSITORY https://github.com/nlohmann/json.git
        GIT_TAG v3.11.3
)
FetchContent_MakeAvailable(json)

FetchContent_Declare(
        spdlog
        GIT_REPOSITORY https://github.com/gabime/spdlog.git
        GIT_TAG v1.15.3
)
set(SPDLOG_BUILD_EXAMPLE OFF CACHE BOOL "Build spdlog examples" FORCE)
set(SPDLOG_BUILD_TESTS OFF CACHE BOOL "Build spdlog tests" FORCE)
set(SPDLOG_BUILD_BENCH OFF CACHE BOOL "Build spdlog benchmarks" FORCE)
set(SPDLOG_FMT_EXTERNAL OFF CACHE BOOL "Use external fmt library" FORCE)
set(SPDLOG_WCHAR_SUPPORT ON CACHE BOOL "Enable wchar_t support" FORCE)

FetchContent_MakeAvailable(spdlog)

FetchContent_Declare(
        minizip-ng
        GIT_REPOSITORY https://github.com/zlib-ng/minizip-ng.git
        GIT_TAG master
)
# Set minizip-ng specific options if needed before populating
set(MZ_BUILD_TESTS OFF CACHE BOOL "Disable minizip-ng tests" FORCE)
set(MZ_BUILD_UNIT_TESTS OFF CACHE BOOL "Disable minizip-ng unit tests" FORCE)
set(MZ_BUILD_FUZZ_TESTS OFF CACHE BOOL "Disable minizip-ng fuzz tests" FORCE)
FetchContent_MakeAvailable(minizip-ng)

### Download and embed cacert.pem ###
set(CACERT_PEM_URL "https://curl.se/ca/cacert.pem")
set(CACERT_PEM "${CMAKE_BINARY_DIR}/certs/cacert.pem")
set(CACERT_HEADER "${CMAKE_BINARY_DIR}/include/cacert_pem.h")

file(MAKE_DIRECTORY "${CMAKE_BINARY_DIR}/certs")
file(MAKE_DIRECTORY "${CMAKE_BINARY_DIR}/include")

if(NOT EXISTS ${CACERT_PEM})
    file(DOWNLOAD
            ${CACERT_PEM_URL}
            ${CACERT_PEM}
            SHOW_PROGRESS
            STATUS status
            LOG log
    )
    list(GET status 0 status_code)
    if(NOT status_code EQUAL 0)
        message(FATAL_ERROR "Failed to download cacert.pem:\n${log}")
    endif()
endif()

add_custom_command(
        OUTPUT ${CACERT_HEADER}
        COMMAND ${CMAKE_COMMAND} -DINPUT=${CACERT_PEM} -DOUTPUT=${CACERT_HEADER} -P ${CMAKE_CURRENT_BINARY_DIR}/embed_pem.cmake
        DEPENDS ${CACERT_PEM}
        COMMENT "Embedding cacert.pem as C++ header"
)

file(WRITE "${CMAKE_CURRENT_BINARY_DIR}/embed_pem.cmake" "
file(READ \"\${INPUT}\" PEM_CONTENT)
string(REPLACE \"\\\\\" \"\\\\\\\\\" PEM_CONTENT \"\${PEM_CONTENT}\")
string(REPLACE \"\\\"\" \"\\\\\\\"\" PEM_CONTENT \"\${PEM_CONTENT}\")
string(REPLACE \"\\n\" \"\\\\n\\\"\\n\\\"\" PEM_CONTENT \"\${PEM_CONTENT}\")
set(HEADER \"#pragma once\\n\\nstatic const char* cacert_pem = \\\"\${PEM_CONTENT}\\\";\\n\")
file(WRITE \"\${OUTPUT}\" \"\${HEADER}\")
")

add_custom_target(embed_cert DEPENDS ${CACERT_HEADER})


# --- Add path for miniz-cpp ---
list(APPEND THIRDPARTY_INCLUDE_DIRS ${CMAKE_CURRENT_SOURCE_DIR}/third_party/miniz-cpp/include)


### Target ###
add_executable(
        ObsidianLauncher
        ${CACERT_HEADER}
        src/main.cpp
        src/VersionMeta.cpp
        src/AssetIndex.cpp
        src/JavaVersion.cpp
        src/Library.cpp
        src/MinecraftJAR.cpp
        src/OperatingSystem.cpp
        src/Rule.cpp
        src/Version.cpp
        src/VersionArguments.cpp
        src/VersionLogging.cpp
        src/OSUtil.cpp
        src/JavaDownloader.cpp
        src/Crypto.cpp
        src/JavaManager.cpp
        src/Utils/Logger.cpp
        src/HttpManager.cpp
)

target_include_directories(ObsidianLauncher PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/include
        ${CMAKE_BINARY_DIR}/include

)

add_dependencies(ObsidianLauncher embed_cert)

target_compile_definitions(ObsidianLauncher PRIVATE HAS_STRING_VIEW)

target_link_libraries(ObsidianLauncher
        PRIVATE
        cpr::cpr
        nlohmann_json::nlohmann_json
        OpenSSL::SSL
        OpenSSL::Crypto
        spdlog::spdlog
        MINIZIP::minizip
)

# --- Copy OpenSSL DLLs on Windows after building the target ---
if (WIN32)
    file(GLOB OPENSSL_DLLS "${OPENSSL_ROOT_DIR}/bin/*.dll") # More robust way to find DLLs
    # If OPENSSL_ROOT_DIR is not set correctly, fallback to your hardcoded path
    if(NOT OPENSSL_DLLS)
        file(GLOB OPENSSL_DLLS "C:/Programs/OpenSSL-Win64/*.dll")
    endif()

    message(STATUS "OpenSSL root dir: ${OPENSSL_ROOT_DIR}") # Check if find_package(OpenSSL) sets this
    message(STATUS "OpenSSL DLLs: ${OPENSSL_DLLS}")

    foreach(_dll IN LISTS OPENSSL_DLLS)
        add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "${_dll}"
                $<TARGET_FILE_DIR:${PROJECT_NAME}>
        )
    endforeach()
endif()

if (TARGET zlib) # Check if the 'zlib' target from zlib-ng exists
    get_target_property(ZLIB_TYPE zlib TYPE)
    if (ZLIB_TYPE STREQUAL "SHARED_LIBRARY" OR ZLIB_TYPE STREQUAL "MODULE_LIBRARY")
        add_custom_command(
                TARGET ObsidianLauncher POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "$<TARGET_FILE:zlib>"  # Source: zlib-ng DLL (e.g., zlib-ngd2.dll)
                # $<TARGET_FILE:...> resolves to the correct name and path
                # for the current build configuration (Debug/Release).
                "$<TARGET_FILE_DIR:ObsidianLauncher>"      # Destination: where ObsidianLauncher.exe is
                COMMENT "Copying zlib-ng DLL to executable directory"
        )
    endif()
else()
    message(WARNING "Target 'zlib' (expected from zlib-ng dependency) not found. Cannot set up DLL copy for it. This is okay if zlib-ng is built statically.")
endif()