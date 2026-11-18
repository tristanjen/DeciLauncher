# Deci Launcher — 开发指南（Agent）

跨平台 Minecraft 启动器：**C#/.NET 10 + Photino.NET** 后端，**Vue 3 + Tailwind CSS + Reka UI** 前端（Lucide 图标、Animate.css 过渡），通过 WebView 原生消息桥通信。前端在 Release 下由内嵌 Kestrel（`127.0.0.1` 随机端口 + token/cookie）提供，Debug 下由 Vite 提供。

当前版本见 `DeciLauncher.csproj` 的 `<Version>`；前端 `UserInterface/package.json` 的 `version` 必须与之一致。

## 目录结构

| 路径 | 说明 |
|---|---|
| `Program.cs` | 入口、单实例、日志、Kestrel/token、窗口与全局异常兜底 |
| `Program.Window.cs` | Photino 窗口配置、消息路由、拖拽/最小化/关闭、扫描触发 |
| `Program.Accounts.cs` | 离线账户、配置目录、迁移、原子保存 |
| `Program.Java.cs` | Java 检测（Windows 走 MinecraftLaunch；macOS/Linux 自行枚举候选） |
| `Program.Games.cs` | 读取 `.minecraft/versions/`、模组信息、图标解析 |
| `Program.Launch.cs` | 启动流水线、进程监控、取消、关闭游戏 |
| `Program.Crash.cs` | 崩溃报告解析与原因归类 |
| `Program.MacWindow.cs` | macOS AppKit 透明窗口补齐（Photino 未实现） |
| `Program.Dpi.cs` / `Program.Native.cs` / `Program.Version.cs` / `Program.LaunchNatives.cs` | DPI、P/Invoke、版本、native 匹配 |
| `Launching/` | 纯函数组件：参数模板、路径映射、WebView 参数、反射 fallback 等 |
| `Log.cs` / `AtomicFile.cs` / `GameLogBuffer.cs` / `GameMessages.cs` | 日志、原子写、游戏输出、消息常量 |
| `UserInterface/` | Vue 3 + Tailwind CSS + Reka UI + Lucide + Vite 前端（`src/views`、`components`、`stores`、`i18n`、`native.ts`） |
| `DeciLauncher.Tests/` | xUnit v4 测试 |
| `packaging/` | 图标、`Info.plist`、`make-dmg.sh` |
| `global.json` | .NET 10 MTP 测试模式配置 |
| `publish.ps1` | 本地发布脚本（前端 + 可构建 RID + ZIP） |
| `.github/workflows/ci.yml` | 前端检查、后端六平台矩阵、各平台安装包 |
| `README*.md` / `docs/` | 用户文档与截图 |

## 构建与验证

```bash
# 后端
dotnet build
dotnet test -c Release        # xUnit v4 + Microsoft.Testing.Platform（见 global.json）

# 前端
cd UserInterface
pnpm install --frozen-lockfile
pnpm test                     # Vitest
pnpm type-check               # vue-tsc
pnpm build

# 版本一致性
node UserInterface/scripts/check-version.mjs

# 发布单个 RID（必须显式指定 csproj，否则会解析 slnx 并尝试发布测试项目）
dotnet publish DeciLauncher.csproj -c Release -r osx-x64
```

- **测试运行器**：xUnit v4 使用 .NET 10 的 MTP 模式，`global.json` 中必须保留 `test.runner = Microsoft.Testing.Platform`；VSTest 模式在 .NET 10 SDK 下不受支持。
- **Node 要求**：Vite 8 / Vitest 5 需要 Node 22.12+（CI 使用 Node 22）。
- **Debug 运行**：先 `cd UserInterface && pnpm dev`，再 `dotnet run --project DeciLauncher.csproj`；Debug 会探测 5173 并校验页面标记。Release 不需要 Vite。

## 运行架构

### 前端 ↔ 后端消息

- 发送：`window.external.sendMessage(JSON.stringify({ type, ...payload }))`（`src/native.ts`）。
- 接收：
  - Windows（WebView2）：监听 `chrome.webview` 的 `message` 事件。
  - macOS/Linux（WKWebView / WebKitGTK）：**调用** `window.external.receiveMessage(callback)` 注册回调。
  - **禁止**写成 `window.external.receiveMessage = dispatch`：这会覆盖 Photino 注入的注册函数，导致所有后端消息静默丢失（`native.test.ts` 已覆盖）。
- 消息类型契约在 `src/messages.ts`；后端消息常量在 `GameMessages.cs`，其余消息在 `Program.Window.cs` 中按 `type` 路由。

### 前端样式与组件

- 样式入口：`UserInterface/src/assets/main.css` 的 `@import "tailwindcss"` + `@theme`；颜色/阴影/动画全部走语义 token（`deci-*`），不要在模板里新增硬编码色值。
- 交互原语：`Controls/` 是对 Reka UI 的薄封装——`Modal` → Dialog、`Notification` → AlertDialog、`Dropdown` → Select、`RangeSlider` → Slider、`Toast` → Toast、列表选择 → RadioGroup。按钮/卡片/输入框等 Reka 没有对应原语的组件保持手写 + Tailwind。
- Reka UI 的浮层默认 portal 到 `body`；`Modal` / `Notification` 显式 portal 到 `#main-card`，避免圆角卡片裁剪丢失。
- 图标统一用 `@lucide/vue` 的按需导入（`import { Play } from '@lucide/vue'`）；品牌/游戏图标（logo、loader、方块、Steve）继续用图片，不要新增手写 SVG 图标。
- Vue `<Transition>` 用 Animate.css 类（`animate__animated animate__fadeIn` 等）；全局 `--animate-duration` 为 `0.2s`。Reka 浮层不要套 Animate.css，继续用 `data-[state]` 的 `deci-*` 动画。
- Tailwind v4 的 important 修饰符是**后缀**（`text-deci-primary-active!`）；不要写 UnoCSS 的前缀形式 `!text-...`。
- Reka UI 的 `SelectItem` **禁止** `value=""`（空值用于清空选择并显示 placeholder）；空值状态由 `Controls/Dropdown.vue` 统一映射为哨兵值。

### 窗口与平台差异

- **Windows**：初始窗口放到屏幕外（`-32000,-32000`），等前端首帧消息后再居中，避免 WebView2 首帧黑底；`WebViewArgumentsBuilder` 的 Chromium 参数**只能**在 Windows 传入。
- **macOS/Linux**：窗口创建时直接居中显示。WebKit 在窗口不可见时会暂停 `requestAnimationFrame`，而前端在 `App.vue` 中等待双 rAF 后才发首条消息；把窗口放屏幕外会触发 15 秒看门狗。
- **macOS 透明**：Photino 4.0.16 的 `SetTransparent` 在 macOS 是空实现。`Program.MacWindow.cs` 在窗口创建后通过 Objective-C 运行时设置 `NSWindow` 透明、`WKWebView.drawsBackground = NO`；`--opaque`/`DECILAUNCHER_OPAQUE=1` 时跳过。
- **单实例**：Windows 命名 Mutex；macOS/Linux 使用 `launcher.lock`（`FileShare.None`；macOS 不调用 `FileStream.Lock`）。

### 配置与路径

| 内容 | Windows | macOS / Linux |
|---|---|---|
| 账户/日志/锁 | `%AppData%\.decilc` | `~/.config/.decilc` |
| 默认游戏目录 | 可执行文件旁 `.minecraft`（便携） | macOS：`~/.decilc/.minecraft`；Linux：可执行文件旁 `.minecraft`（便携） |

- macOS 旧目录 `~/Library/Application Support/.decilc` 的 `accounts.json` / `.bak` 会在启动时迁移到 `~/.config/.decilc`（`MigrateMacAccounts`）。
- 日志：`logs/latest.log`，滚动 3 份；配置目录不可写时回退到可执行文件旁 `logs/`。
- 游戏目录解析：可执行文件旁的 `.minecraft` 存在时优先（便携）；否则 macOS 用 `~/.decilc/.minecraft`，Windows/Linux 仍用便携路径。
- 路径必须用 `Path.Combine` / 现有平台抽象，不要硬编码 `\` / `/`。

### Release 裁剪与序列化

- `PublishTrimmed=true`，默认 full 裁剪。
- **必须保留** `<TrimmerRootAssembly Include="Deci Launcher" />`：前端消息依赖反射序列化匿名对象，裁掉主程序集会使其退化为 `{}`。
- `JsonSerializerIsReflectionEnabledByDefault=true` 是显式设计，不要删除。
- 发布为自包含单文件；普通 build/test 保持框架依赖。

### 打包

- **Windows**：`dotnet publish -r win-x64` → 单文件 `.exe`；CI 重命名上传。
- **Linux**：CI 生成 `.deb` / `.rpm` / `.AppImage`。
- **macOS**：CI 组装 `DeciLauncher.app`（`Info.plist` + `AppIcon.icns`，版本由 `PlistBuddy` 从 csproj 注入），再调用 `packaging/make-dmg.sh` 生成 DMG：
  - 600×320 窗口，图标整体居中，含 `Applications -> /Applications` 快捷方式；
  - 使用 `packaging/icon-macos.png`（640×640，内容约 80%，符合 macOS 图标网格）；
  - Finder 布局失败时脚本会跳过布局但保留 .app + Applications 链接。
- `publish.ps1` 只产出 ZIP；`.app`/DMG 由 CI 或 `make-dmg.sh` 负责。
- 版本号单一来源：csproj `<Version>`；`package.json` 必须同步；`Info.plist` 由 CI 注入。
- **macOS 最低版本 15.0**：`packaging/Info.plist` 的 `LSMinimumSystemVersion` 必须与 README 的系统要求及 .NET 10 / Tailwind v4 支持范围一致。

## 依赖与升级

- 升级前先看 `dotnet list <project> package --outdated --highest-minor` 与 `pnpm outdated`。
- **TypeScript 保持 6.0.x**：Volar/vue-tsc 的 peer 依赖要求 `<7`，升到 7 会导致 `vue-tsc` 崩溃。
- **Microsoft.Extensions.FileProviders.Embedded 保持 10.0.x**：11.x 面向 net11.0；本项目是 net10.0。
- **Tailwind CSS 保持 4.x**：入口 `src/assets/main.css` 使用 `@import "tailwindcss"` + `@theme`；`@tailwindcss/vite` 与 Vite 8 兼容。App 最低 macOS 15.0（`packaging/Info.plist`），满足 Tailwind v4 的 Safari 16.4+ 要求。
- **Reka UI 保持 2.x**：只提供无头行为与 ARIA，不携带样式；`SelectItem` 的空字符串限制见上文。
- **@lucide/vue 1.x / Animate.css 4.x**：图标按需导入；Animate.css 用于 Vue Transition，不用于 Reka 浮层的 `data-state` 动画。
- xunit v4 依赖 MTP 模式；如果 `dotnet test` 报 MTP/VSTest 错误，检查 `global.json` 是否被删除。
- 大版本升级单独提交，并在 PR/提交信息中说明兼容性取舍。

## 设计与 UI 技能路由

本项目 UI 是 **Vue 3 + Tailwind CSS + Reka UI**（Lucide 图标、Animate.css 过渡），不是 React/shadcn。按任务显式选择技能：

| 任务 | 使用 | 禁止/避免 |
|---|---|---|
| 新页面、组件、交互、布局 | `frontend-design` + `ui-ux-pro-max` | `ui-styling` |
| 配色、字体、UX/可访问性规则 | `ui-ux-pro-max` | — |
| Design token / CSS 变量 | `design-system` | 不要引入 shadcn 的 token 体系 |
| 品牌手册、语气、logo 规范 | `brand` | — |
| 发版 banner、商店图、宣传物料 | `banner-design` | `design`（依赖外部 AI 图像 API） |
| 图表/统计可视化 | `dataviz` | `slides` |
| 截图、交互、视觉验收 | `webapp-testing` | — |
| 需求/方案压力测试 | `grilling` | `brainstorming`（会抢触发） |
| 实现计划 | `writing-plans` | — |
| 按计划实现 | `subagent-driven-development`（有测试/CI 的任务） | — |
| 声称完成前 | `verification-before-completion` | — |

硬规则：

- **不要**运行 `npx shadcn@latest init` 或引入 shadcn；样式一律用 Tailwind CSS + `src/assets/main.css` 的 `@theme` token。
- 不要用 `ui-styling` 接管 UI 实现；它只作为组件模式/可访问性参考。
- 保持绿色极简主题；新页面先读 `src/assets/main.css` 和现有 `views/`，沿用现有 token/类名习惯。
- 用户可见文案必须走 `src/i18n/zh-CN.ts` 与 `en-US.ts`，不要硬编码。
- 修改 UI 后至少跑 `pnpm test` 与 `pnpm type-check`；涉及后端时跑 `dotnet test -c Release`。
- 不要声称「完成/通过」而没有实际命令输出（见 `verification-before-completion`）。

## 代码风格

- C#：`Nullable` 开启、`AnalysisLevel=latest`、隐式 using；保持现有 partial class 拆分习惯。
- 前端：Vue 3 `<script setup>`、TypeScript、Tailwind CSS、Reka UI；使用 `stores/` 的共享状态与 `native.ts` 的消息桥。
- 跨平台路径：使用 `Path.Combine` / `LibraryPathMapper` / 平台抽象，不要硬编码分隔符。
- Photino/native 互操作：保持 `Program.Native.cs` 与 `Program.MacWindow.cs` 的 P/Invoke 声明集中、可审查。

## 不要做

- 不要提交 `bin/`、`obj/`、`Resources/wwwroot/`、`Release/` 构建产物。
- 不要删除 `TrimmerRootAssembly Include="Deci Launcher"`，否则发布版前端消息会变成 `{}`。
- 不要在非 Windows 调用 `SetBrowserControlInitParameters`（Photino 会把它当 WebKit JSON 解析并触发 SIGABRT）。
- 不要把 `window.external.receiveMessage` 赋值；必须调用它注册回调。
- 不要把 macOS/Linux 窗口初始位置放到屏幕外（WebKit 会暂停 rAF，触发前端看门狗）。
- 不要重新引入 macOS 自动不透明回退；透明由 `Program.MacWindow.cs` 负责，`--opaque` 仅作回退。
- 不要改默认配置目录/游戏目录而不更新迁移逻辑、README 与日志路径。
- 不要改 CI 的六平台矩阵或版本来源而不更新对应校验脚本；`.app` 版本注入与 DMG 布局属于发布流程的一部分。
- 不要重新引入 UnoCSS；样式统一走 Tailwind CSS。
- 不要给 Reka UI `SelectItem` 传空字符串 `value`；空值状态通过 `Controls/Dropdown.vue` 的哨兵值处理。
- 不要新增手写 SVG/图片图标（品牌与游戏图标除外），统一用 `@lucide/vue`。
- 不要给 Reka 浮层套 Animate.css；页面过渡用 Animate.css，浮层保持 `data-state` 动画。
- 不要引入 shadcn，或用 `ui-styling` 接管实现。

## 完成前检查清单

1. 后端：`dotnet build` + `dotnet test -c Release`。
2. 前端：`pnpm test` + `pnpm type-check` + `pnpm build`。
3. 版本：`node UserInterface/scripts/check-version.mjs`。
4. 改了平台相关代码：在对应平台实际启动；改了 UI：用 `webapp-testing` 在 Chromium 与 WebKit 跑关键流程；改了 macOS 打包：实际挂载 DMG 检查 `.app`、`Applications` 链接与图标布局。
5. 改了 CI/YAML：本地做 YAML 语法检查（如 `ruby -e 'require "yaml"; YAML.load_file(...)'`）。
6. 改了用户可见行为/路径/功能：同步更新 `README.md`、`README.zh-CN.md` 与本文件。
7. 任务完成后按当前用户的指示提交并推送。
