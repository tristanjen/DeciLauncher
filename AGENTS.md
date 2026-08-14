# AGENTS.md

## 推送规则(用户指令,必须遵守)

- **未经用户明确批准,禁止执行 `git push`(含 `git push --force`、推送任何分支/标签)。**
- 用户说"推送"或明确授权时方可推送;任何含推送意图但未明确授权的请求,先询问用户。
- 本地 commit 不受此限制;但推送前如远程有新提交,需先向用户报告。

## 项目速览

- 跨平台 Minecraft 启动器:C#/.NET 10 + Photino.NET(后端)+ Vue 3 + Vite(前端)
- 构建:`dotnet build`;测试:`dotnet test`(DeciLauncher.Tests,xUnit v3,位于 slnx);
  Release 发布见 `publish.ps1`(注意:必须显式指定 `DeciLauncher.csproj`,无参数 publish 作用于 slnx 会触发 NETSDK1151);前端:`cd UserInterface && pnpm install && pnpm build`
- 后端结构:`Program.*.cs` 分部类(启动/窗口/账户/游戏/Java/DPI/Native/崩溃分析);
  `Launching/` 目录为可测纯函数静态类(CommandLineBuilder 引号规则、ArgumentTemplateEngine 参数模板、
  LibraryPathMapper、MinecraftLaunchFallbacks 反射收敛点);
  根目录工具类:GameMessages(消息常量)、Log([Conditional("DEBUG")] 日志)、AtomicFile(原子写入)
- 前端消息协议经 `window.external` JSON 桥(native.ts),类型契约集中在 `src/messages.ts`
  (RequestMap/ResponseMap)——**前后端消息形状改动必须同步两侧**
- 运行时消息分发入口见 `Program.Window.cs`;日志/调试:后端 DEBUG 构建输出到控制台
- 下载源偏好(镜像开关):前端 `stores/downloadSource.ts`(localStorage)→ `set-download-source` 消息
  → 后端 `DownloadManager.IsEnableMirror`;两侧默认值需保持一致(official-first)
- 构建注意:主项目 csproj 显式排除 `DeciLauncher.Tests/**`(防默认 glob 重复编译);
  `SelfContained` 仅在 `IsPublishing` 时生效(测试项目引用依赖此条件)
