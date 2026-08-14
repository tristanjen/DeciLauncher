# Deci Launcher

[English](README.md) | [简体中文](README.zh-CN.md)

基于 C#/.NET 10 + Photino.NET + Vue 3 构建的跨平台 Minecraft 启动器。

## 功能特性

- **跨平台** — Windows、macOS、Linux（x64 和 ARM64）
- **离线账户** — 创建和管理离线 Minecraft 账户
- **Java 自动检测** — 扫描系统中已安装的 Java 运行时，按版本自动匹配最佳版本；手动选择过低版本时给出警告
- **版本扫描** — 读取 `.minecraft/versions/` 目录，自动检测原版和模组（Fabric/Forge/NeoForge/Quilt）版本
- **版本隔离** — 版本目录自带 `mods/` 或 `saves/` 时，游戏数据隔离在版本目录内
- **下载源切换** — 可在 BMCLAPI 镜像源与 Mojang 官方源之间切换
- **分阶段启动进度** — 启动按钮实时显示阶段（解析版本 → 查找 Java → 启动 → 等待窗口）
- **崩溃分析** — 游戏异常退出时自动解析最新崩溃报告，中文/英文解释原因（内存/显卡驱动/Mod 冲突/Java 版本等）
- **单文件发布** — 自包含 `.exe`，无需外部 DLL
- **简洁界面** — 绿色主题极简 UI，带过渡动画

## 技术栈

| 层级 | 技术 |
|------|------|
| 后端 | C# (.NET 10)、Photino.NET、MinecraftLaunch |
| 前端 | Vue 3 + TypeScript + Tailwind CSS v4 + Vite |
| 桌面 | Photino.NET（Windows 上使用 WebView2，macOS/Linux 上使用 WebKit） |
| 构建 | .NET SDK + pnpm |

## 开发指南

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/)（v18+）
- [pnpm](https://pnpm.io/)

### 安装

```bash
# 安装前端依赖
cd UserInterface
pnpm install

# 调试模式运行（Vite 开发服务器 + .NET 调试）
pnpm dev & dotnet run
```

### 构建

```bash
# 构建前端
cd UserInterface && pnpm build && cd ..

# 构建后端（Debug）
dotnet build

# 运行测试（xUnit v3，DeciLauncher.Tests）
dotnet test

# 发布（Release，单文件，自包含）
# 注意：必须显式指定 csproj —— 不带参数的 dotnet publish 会解析 .slnx 并尝试发布测试项目（NETSDK1151）
dotnet publish DeciLauncher.csproj -c Release -r win-x64
```

## 使用的开源项目

| 项目 | 用途 | 许可证 |
|------|------|--------|
| [.NET](https://github.com/dotnet/runtime) | 运行时和 SDK | MIT |
| [ASP.NET Core](https://github.com/dotnet/aspnetcore) | 内嵌文件服务器 | MIT |
| [Photino.NET](https://github.com/tryphotino/photino.NET) | 桌面窗口框架 | Apache-2.0 |
| [MinecraftLaunch](https://github.com/Lunova-Studio/MinecraftLaunch) | Minecraft 启动核心 | MIT |
| [Vue](https://github.com/vuejs/core) | 前端框架 | MIT |
| [Vite](https://github.com/vitejs/vite) | 前端构建工具 | MIT |
| [Tailwind CSS](https://github.com/tailwindlabs/tailwindcss) | CSS 框架 | MIT |
| [TypeScript](https://github.com/microsoft/TypeScript) | 类型化 JavaScript | Apache-2.0 |
| [xUnit](https://github.com/xunit/xunit) | 测试框架 | Apache-2.0 |
| [pnpm](https://github.com/pnpm/pnpm) | 包管理器 | MIT |

## 许可证

[GPL-3.0](LICENSE)
