# Deci Launcher

[English](README.md) | [简体中文](README.zh-CN.md)

A cross-platform Minecraft Launcher built with C#/.NET 10 + Photino.NET + Vue 3.

## Features

- **Cross-platform** — Windows, macOS, Linux (x64 & ARM64)
- **Offline accounts** — Create and manage offline Minecraft accounts
- **Auto Java detection** — Scans system for installed Java runtimes, auto-matches the best one per version; warns when a manually selected Java is below the version requirement
- **Version scanning** — Reads `.minecraft/versions/` directory, auto-detects vanilla and modded (Fabric/Forge/NeoForge/Quilt) versions
- **Version isolation** — When a version folder has its own `mods/` or `saves/`, game data is kept inside the version folder
- **Download source** — Switchable between BMCLAPI mirror and official Mojang sources
- **Staged launch progress** — Live stage text (parse → Java → launch → waiting) on the launch button
- **Crash analysis** — Automatically parses the latest crash report on abnormal exit and explains the cause (memory / graphics driver / mod conflicts / Java version / …) in Chinese or English
- **Single-file publish** — Self-contained `.exe` with no external DLLs
- **Clean UI** — Green-themed minimal interface with animated transitions

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | C# (.NET 10), Photino.NET, MinecraftLaunch |
| Frontend | Vue 3 + TypeScript + Tailwind CSS v4 + Vite |
| Desktop | Photino.NET (WebView2 on Windows, WebKit on macOS/Linux) |
| Build | .NET SDK + pnpm |

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) (v18+)
- [pnpm](https://pnpm.io/)

### Setup

```bash
# Install frontend dependencies
cd UserInterface
pnpm install

# Run in development mode (Vite dev server + .NET debug)
pnpm dev & dotnet run
```

### Build

```bash
# Build frontend
cd UserInterface && pnpm build && cd ..

# Build backend (Debug)
dotnet build

# Run tests (xUnit v3, DeciLauncher.Tests)
dotnet test

# Publish (Release, single-file, self-contained)
# Note: pass the csproj explicitly — a bare `dotnet publish` resolves the .slnx
# and would try to publish the test project (NETSDK1151)
dotnet publish DeciLauncher.csproj -c Release -r win-x64
```

## Used Open Source Projects

| Project | Purpose | License |
|---------|---------|---------|
| [.NET](https://github.com/dotnet/runtime) | Runtime & SDK | MIT |
| [ASP.NET Core](https://github.com/dotnet/aspnetcore) | Embedded file server | MIT |
| [Photino.NET](https://github.com/tryphotino/photino.NET) | Desktop windowing | Apache-2.0 |
| [MinecraftLaunch](https://github.com/Lunova-Studio/MinecraftLaunch) | Minecraft launch core | MIT |
| [Vue](https://github.com/vuejs/core) | Frontend framework | MIT |
| [Vite](https://github.com/vitejs/vite) | Frontend build tool | MIT |
| [Tailwind CSS](https://github.com/tailwindlabs/tailwindcss) | CSS framework | MIT |
| [TypeScript](https://github.com/microsoft/TypeScript) | Typed JavaScript | Apache-2.0 |
| [xUnit](https://github.com/xunit/xunit) | Test framework | Apache-2.0 |
| [pnpm](https://github.com/pnpm/pnpm) | Package manager | MIT |

## License

[GPL-3.0](LICENSE)
