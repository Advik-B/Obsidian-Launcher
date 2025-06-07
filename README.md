# Obsidian Launcher

## The Origin Story: From C++ to C#

This project, Obsidian Launcher, is a custom 'cracked' Minecraft launcher.

It didn't start its life in C#, though. My first attempt was in C++, This is the last commit I had made in C++ https://github.com/Advik-B/Obsidian-Launcher/tree/ff27925ef0075a02787c2754d71c0096c27b33f1.

Why the switch? Let's just say C++ development, while powerful, presented a series of... *[character-building](./Images/rage-commits.png)* experiences that eventually led me to the more streamlined environment of C# and .NET.

**A few highlights from the C++ trenches:**

*   **The Great Library Hunt & CMake Wars:** Simply getting a reliable compression and archive library integrated felt like a quest in itself. I wrestled with CMake for *days* just to get a clean build across different setups. The hours spent debugging build scripts could have been spent building features!
*   **The SSL Certificate Fiasco:** Remember OpenSSL's delightful inability to easily read system certificates on certain platforms? I do. I ventured down the rabbit hole of embedding CA certificates (`cacert.pem`) directly into the executable. And you know what? It *mostly* worked! Except... for that *one specific URL* that would mysteriously fail. The kicker? Automated tests for that exact URL would pass every single time. That little adventure cost me a good chunk of my sanity (and probably some hair). In a moment of sheer frustration, SSL verification was disabled altogether.

It was clear that for this project, the verbosity and the "everything-is-manual" nature of C++ was becoming more of a hindrance than a help for the features I wanted to build efficiently. So, I threw it all away and made a fresh start with the power and convenience of C# and the rich .NET ecosystem.

## What Obsidian Launcher Aims To Do

This Project aims to achieve the core functionalities of a modern Minecraft launcher:

*   Fetching and managing official Minecraft versions.
*   Handling Java runtime environments (discovery, download, extraction from Mojang & Adoptium).
*   Downloading and verifying all necessary game assets.
*   Managing game libraries, including native ones for LWJGL.
*   Correctly constructing classpaths and all JVM/game arguments based on version manifests.
*   Launching the game!

## Current Status & Features

This project is actively in development. Here's what's working or in progress:

*   **Version Manifests**: Fetches and parses Mojang's version data.
*   **Java Management**: Finds existing Java, downloads from Mojang/Adoptium, and extracts archives (`.zip` fully supported, `.tar.gz` is a TODO for robust cross-platform Java).
*   **Asset Management**: Downloads and verifies game assets to the standard hashed structure.
*   **Library Management**: Downloads, verifies, and extracts libraries, including OS-specific natives. Rule evaluation for libraries is in place.
*   **Argument & Classpath Building**: Constructs the classpath and initial JVM/game arguments with placeholder replacement.
*   **Game Launch**: Successfully launches the Minecraft process with captured output.
*   **Logging**: Uses Serilog for detailed console and file logging.


## Why C# for This Port?

*   **Rich Standard Library**: `HttpClient`, `System.IO.Compression.ZipFile`, `System.Security.Cryptography`, `System.Text.Json` – so much is built-in and just works.
*   **NuGet Package Ecosystem**: For anything not in the BCL, NuGet is a breeze.
*   **Productivity**: Less boilerplate, more focus on the launcher's logic.
*   **Modern Language Features**: Async/await, LINQ, etc., make for cleaner, more maintainable code.
*   **Sanity Preservation**: Fewer days spent fighting build systems and low-level library quirks.

## Project Structure

```
ObsidianLauncher/
├── ObsidianLauncher.sln
└── ObsidianLauncher/
├── ObsidianLauncher.csproj
├── Program.cs
├── LauncherConfig.cs
├── Models/
│   └── (Model classes (For Schema) like MinecraftVersion.cs, Library.cs, etc.)
├── Enums/
│   └── (Enum files like OperatingSystemType.cs)
├── Services/
│   └── (Service classes like HttpManagerService.cs, JavaManager.cs, etc.)
└── Utils/
└── (Utility classes like CryptoUtils.cs, LoggerSetup.cs, etc.)
```

## Prerequisites

*   .NET 9.0 SDK (or a compatible newer version)

## Building and Running

1.  Clone the repository.
2.  Restore NuGet packages:
    ```
    dotnet restore "Obsidian Launcher.csproj"
    ```
3.  Build: 
    ```
    dotnet build "Obsidian Launcher.csproj" -c Release
    ```
4.  Run: `bin/Release/net9.0/Obsidian Launcher.exe`
    (Data will be stored in `.ObsidianLauncher` by default)

## TODO / Future Enhancements

*   **Full Authentication**: Implement Microsoft Account (MSA) login. (Currently supports offline/placeholder auth).
*   **UI**: This is a console app. A GUI would be a major step up!
*   **Profile/Instance Management**.
*   **Mod Management**.
*   **Robust TAR.GZ Extraction**: For Java runtimes on Linux/macOS from Adoptium.
*   **Complete all Argument Placeholder Replacements**.
*   **Unit Tests!**
*   ...and much more!

## Contributing

Found a bug? Have an idea? Feel free to open an issue or submit a pull request.

## License

This project is under the [GPL v3 License](LICENSE.txt)