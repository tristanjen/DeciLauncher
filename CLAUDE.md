# Deci Launcher — 开发指南

跨平台 Minecraft 启动器：C#/.NET 10 + Photino.NET 后端，Vue 3 + UnoCSS 前端，通过内嵌 WebView 通信。

## 目录

- `Program*.cs` — 启动器后端（启动、账户、Java 检测、崩溃分析、窗口）
- `Launching/` — 启动流程
- `UserInterface/` — Vue 3 + UnoCSS 前端（Vite）
- `DeciLauncher.Tests/` — xUnit v3 测试
- `packaging/` — 打包资源
- `publish.ps1`、`.github/workflows/ci.yml` — 发布与 CI

## 构建与验证

- 后端：`dotnet build` / `dotnet test`
- 前端：`cd UserInterface && pnpm install --frozen-lockfile && pnpm test && pnpm build`
- 类型检查：`cd UserInterface && pnpm type-check`
- 版本一致性：`UserInterface/package.json` 的 `version` 必须与 `DeciLauncher.csproj` 的 `<Version>` 同步（CI 有 `UserInterface/scripts/check-version.mjs`）
- 发布：`publish.ps1` / CI 六平台矩阵（win/linux/osx × x64/arm64）

## 设计与 UI 技能路由

本项目 UI 是 **Vue 3 + UnoCSS**，不是 React/Tailwind/shadcn。按任务显式选择技能：

| 任务 | 使用 | 禁止/避免 |
|---|---|---|
| 新页面、组件、交互、布局 | `frontend-design` + `ui-ux-pro-max` | `ui-styling` |
| 配色、字体、UX/可访问性规则 | `ui-ux-pro-max` | — |
| Design token / CSS 变量 | `design-system` | 不要执行 Tailwind 集成 |
| 品牌手册、语气、logo 规范 | `brand` | — |
| 发版 banner、商店图、宣传物料 | `banner-design` | `design`（依赖外部 AI 图像 API） |
| 图表/统计可视化 | `dataviz` | `slides` |
| 截图、交互、视觉验收 | `webapp-testing` | — |
| 需求/方案压力测试 | `grilling` | `brainstorming`（会抢触发） |
| 实现计划 | `writing-plans` | — |
| 按计划实现 | `subagent-driven-development`（有测试/CI 的任务） | — |
| 声称完成前 | `verification-before-completion` | — |

硬规则：

- **不要**在 DeciLauncher 里运行 `npx shadcn@latest init` 或引入 Tailwind/shadcn；样式一律用 UnoCSS + `src/assets/main.css` 的现有变量。
- 不要用 `ui-styling` 接管 UI 实现；它只作为组件模式/可访问性参考。
- 保持现有绿色极简主题；新页面先读 `src/assets/main.css` 和现有 `views/`，沿用现有 token/类名习惯。
- 修改 UI 后至少跑 `pnpm test` 与 `pnpm type-check`；涉及后端时跑 `dotnet test`。
- 不要声称「完成/通过」而没有实际命令输出（见 `verification-before-completion`）。

## 代码风格

- C#：`Nullable` 开启、`AnalysisLevel=latest`、隐式 using；保持现有 partial class 拆分习惯。
- 前端：Vue 3 `<script setup>`、TypeScript、UnoCSS；文案走 `src/i18n/`（zh-CN / en-US），不要硬编码用户可见字符串。
- 跨平台路径：使用现有 `LibraryPathMapper` / 平台抽象，不要硬编码 `\` 或 `/`。

## 不要做

- 不要提交 `bin/`、`obj/`、`Resources/wwwroot/` 构建产物。
- 不要改 CI 的六平台矩阵或版本来源而不更新对应校验脚本。
