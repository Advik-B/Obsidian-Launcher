##  Obsidian Launcher

![GitHub stars](https://img.shields.io/github/stars/Advik-B/Obsidian-Launcher?style=for-the-badge)
![GitHub last commit](https://img.shields.io/github/last-commit/Advik-B/Obsidian-Launcher?style=for-the-badge)
![GitHub issues](https://img.shields.io/github/issues/Advik-B/Obsidian-Launcher?style=for-the-badge)
![License](https://img.shields.io/github/license/Advik-B/Obsidian-Launcher?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-9.0-blueviolet?style=for-the-badge)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey?style=for-the-badge)

---

## 🌱 The Origin Story: From C++ to C\#

This project, **Obsidian Launcher**, is a custom *'cracked'* Minecraft launcher.

It didn't start its life in C#, though. My first attempt was in **C++**.
👉 [Click here to browse the last C++ commit](https://github.com/Advik-B/Obsidian-Launcher/tree/ff27925ef0075a02787c2754d71c0096c27b33f1)

Why the switch? Let’s just say C++ development, while powerful, presented a series of... *[character-building](./Images/rage-commits.png)* experiences that eventually led me to the more streamlined environment of **C#** and **.NET**.

### 🔍 A few highlights from the C++ trenches:

* ⚔️ **The Great Library Hunt & CMake Wars**: Simply getting a reliable compression and archive library integrated felt like a quest. I wrestled with CMake for *days* just to get a clean build across setups. Hours spent debugging build scripts could've built actual features! 

* 🔐 **The SSL Certificate Fiasco**: Remember OpenSSL’s lovely inability to easily read system certs on some platforms? I do. I embedded `cacert.pem` manually. It *mostly* worked—except for one URL that always failed outside of tests. The kicker? Tests passed *every single time*. Eventually, I gave up and disabled SSL verification altogether.

It was clear that for this project, the *everything-is-manual* nature of C++ was a roadblock. So, I ditched it and started fresh with **C#** 

---

## 🎯 What Obsidian Launcher Aims To Do

This project aims to replicate the key features of a modern Minecraft launcher:

* 🧩 Fetching and managing official Minecraft versions.
* ☕ Handling Java runtimes (discovery, download, extraction).
* 🧱 Downloading & verifying all required game assets.
* 📚 Managing libraries, including native ones for LWJGL.
* 🧵 Building classpaths and JVM/game arguments.
* ▶️ Launching the game!

---

## 🔧 Current Status & Features

Actively in development 🔄
Here's what's working or in progress:

* 📜 **Version Manifests**: Fetches and parses Mojang's data.
* ☕ **Java Management**: Finds Java, downloads, extracts archives (`.zip` ✅, `.tar.gz` 🔜).
* 🎨 **Asset Management**: Downloads and verifies game assets.
* 📦 **Library Management**: Handles downloads, verification, extraction + native rules.
* 🧠 **Argument & Classpath Builder**: Fully functional with placeholder support.
* 🎮 **Game Launch**: Successfully launches Minecraft with output capture.
* 📋 **Logging**: Serilog-based console and file logging.

---

## 💻 Why C\#

* 📚 **Rich Standard Library** – `HttpClient`, `System.IO.Compression.ZipFile`, and more.
* 📦 **NuGet Ecosystem** – Easy, fast package management.
* ⚡ **Productivity** – Less boilerplate, more logic.
* 🧼 **Modern Features** – Async/await, LINQ, clean syntax.
* 🧘 **Sanity Preservation** – No more build system nightmares.

---

## 🗂️ Project Structure

```
ObsidianLauncher/
├── ObsidianLauncher.sln
└── ObsidianLauncher/
    ├── ObsidianLauncher.csproj
    ├── Program.cs
    ├── LauncherConfig.cs
    ├── Models/
    │   └── (e.g., MinecraftVersion.cs, Library.cs)
    ├── Enums/
    │   └── (e.g., OperatingSystemType.cs)
    ├── Services/
    │   └── (e.g., HttpManagerService.cs, JavaManager.cs)
    └── Utils/
        └── (e.g., CryptoUtils.cs, LoggerSetup.cs)
```

---

## 📦 Prerequisites

* .NET 9.0 SDK (or newer) ✅

---

## 🛠️ Building and Running

1. 🧬 Clone the repository.
2. 🔄 Restore packages:

   ```bash
   dotnet restore "Obsidian Launcher.csproj"
   ```
3. 🏗️ Build it:

   ```bash
   dotnet build "Obsidian Launcher.csproj" -c Release
   ```
4. 🚀 Run it:
   `bin/Release/net9.0/Obsidian Launcher.exe`
   (Data will be stored in `.ObsidianLauncher`)

---

## 🔮 TODO / Future Enhancements

* 🔐 **Authentication**: Add Microsoft login (only offline mode supported currently).
* 🖼️ **UI**: Build a GUI! (Console ≠ user-friendly 😅)
* 📁 **Profile Management**
* 🧩 **Mod Management**
* 🧰 **TAR.GZ Support** for Linux/macOS Java runtimes
* 🧠 **Placeholder Replacements**: Finish ‘em all.
* 🧪 **Unit Tests**
* 💡 And lots more!

---

## 🤝 Contributing

Found a `🐞bug` or have an `💡idea`?
Open an issue or PR — all help is appreciated!

---

## 📜 License

This project is licensed under the [GPL v3 License](LICENSE.txt) 📄

---
