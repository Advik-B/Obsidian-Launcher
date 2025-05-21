cmake_minimum_required(VERSION 4.0)
project(MinecraftLauncher)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

include(FetchContent)

# Platform-specific OpenSSL hints
if(WIN32)
    # Windows-specific paths
    list(APPEND CMAKE_PREFIX_PATH "C:/Programs/OpenSSL-Win64")
elseif(APPLE)
    # macOS paths
    list(APPEND CMAKE_PREFIX_PATH "/usr/local/opt/openssl" "/opt/homebrew/opt/openssl")
endif()


# Find OpenSSL
find_package(OpenSSL REQUIRED)
message(STATUS "OpenSSL found: ${OPENSSL_FOUND}")
message(STATUS "OpenSSL version: ${OPENSSL_VERSION}")
message(STATUS "OpenSSL include dir: ${OPENSSL_INCLUDE_DIR}")
message(STATUS "OpenSSL libraries: ${OPENSSL_LIBRARIES}")

### CPR (with OpenSSL) ###
set(CPR_ENABLE_SSL ON)
set(CPR_FORCE_OPENSSL_BACKEND ON)
set(CURL_USE_OPENSSL ON)

FetchContent_Declare(
        cpr
        GIT_REPOSITORY https://github.com/libcpr/cpr.git
        GIT_TAG master
)
FetchContent_MakeAvailable(cpr)

### nlohmann/json ###
FetchContent_Declare(
        json
        GIT_REPOSITORY https://github.com/nlohmann/json.git
        GIT_TAG v3.12.0
)
FetchContent_MakeAvailable(json)

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

# Custom command to convert PEM to C++ header
add_custom_command(
        OUTPUT ${CACERT_HEADER}
        COMMAND ${CMAKE_COMMAND} -DINPUT=${CACERT_PEM} -DOUTPUT=${CACERT_HEADER} -P ${CMAKE_CURRENT_BINARY_DIR}/embed_pem.cmake
        DEPENDS ${CACERT_PEM}
        COMMENT "Embedding cacert.pem as C++ header"
)

# Write the script inline during configure time
file(WRITE "${CMAKE_CURRENT_BINARY_DIR}/embed_pem.cmake" "
file(READ \"\${INPUT}\" PEM_CONTENT)
string(REPLACE \"\\\\\" \"\\\\\\\\\" PEM_CONTENT \"\${PEM_CONTENT}\")
string(REPLACE \"\\\"\" \"\\\\\\\"\" PEM_CONTENT \"\${PEM_CONTENT}\")
string(REPLACE \"\\n\" \"\\\\n\\\"\\n\\\"\" PEM_CONTENT \"\${PEM_CONTENT}\")
set(HEADER \"#pragma once\\n\\nstatic const char* cacert_pem = \\\"\${PEM_CONTENT}\\\";\\n\")
file(WRITE \"\${OUTPUT}\" \"\${HEADER}\")
")

add_custom_target(embed_cert DEPENDS ${CACERT_HEADER})

### Target ###
add_executable(
        MinecraftLauncher
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
        src/Http.cpp  # Ensure it's treated as a source
)

target_include_directories(MinecraftLauncher PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/include
        ${CMAKE_BINARY_DIR}/include
)

add_dependencies(MinecraftLauncher embed_cert)

target_compile_definitions(MinecraftLauncher PRIVATE HAS_STRING_VIEW)

target_link_libraries(MinecraftLauncher
        PRIVATE cpr::cpr
        nlohmann_json::nlohmann_json
        OpenSSL::SSL
        OpenSSL::Crypto
)


# --- Copy OpenSSL DLLs on Windows after building the target ---
if (WIN32)
    # Glob all DLLs from your OpenSSL install
    file(GLOB OPENSSL_DLLS
            "C:/Programs/OpenSSL-Win64/*.dll"
    )

    message(STATUS "OpenSSL root dir: ${OPENSSL_ROOT_DIR}")
    message(STATUS "OpenSSL DLLs: ${OPENSSL_DLLS}")

    # For each DLL, copy it to the executable’s folder
    foreach(_dll IN LISTS OPENSSL_DLLS)
        add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "${_dll}"
                $<TARGET_FILE_DIR:${PROJECT_NAME}>
        )
    endforeach()
endif()
