# AGENTS.md

## 推送规则(用户指令,必须遵守)

- **未经用户明确批准,禁止执行 `git push`(含 `git push --force`、推送任何分支/标签)。**
- 用户说"推送"或明确授权时方可推送;任何含推送意图但未明确授权的请求,先询问用户。
- 本地 commit 不受此限制;但推送前如远程有新提交,需先向用户报告。

## 项目速览

- 跨平台 Minecraft 启动器:C#/.NET 10 + Photino.NET(后端)+ Vue 3 + Vite(前端)
- 构建:`dotnet build`(Release 发布见 `publish.ps1`);前端: `cd UserInterface && pnpm install && pnpm build`
- 后端为 `Program.*.cs` 分部类;前端消息协议经 `window.external` JSON 桥(native.ts)
- 日志/调试:后端 DEBUG 构建输出到控制台;运行时消息类型见 `Program.Window.cs`
