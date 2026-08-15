// 前端 ↔ 后端消息协议类型契约。
// 与 C# 后端序列化对象（Program.*.cs 中 JsonSerializer.Serialize 的匿名对象）一一对应，
// 修改任一侧消息形状时需同步另一侧。
// 注意：ResponseMap 的 payload 不含 type 字段（native.ts 分发时已剥离）。

import type { JavaEntry } from './stores/java'
import type { GameEntry } from './stores/games'
import type { AccountEntry } from './stores/accounts'
import type { DownloadSource } from './stores/downloadSource'

/** 前端 → 后端请求消息 */
export interface RequestMap {
  'set-language': { language: string }
  'set-download-source': { source: DownloadSource }
  'scan-java': { force?: boolean } | undefined
  'scan-games': { path: string }
  'list-accounts': undefined
  'create-offline-account': { name: string }
  'delete-offline-account': { uuid: string }
  'pick-game-path': undefined
  /** 关于页：请求应用信息（后端回发 app-info） */
  'get-app-info': undefined
  'launch-game': {
    gameId: string
    accountUuid: string
    javaPath: string
    maxMemory: number
    minecraftPath: string
  }
  'close-game': undefined
  'cancel-launch': undefined
  'drag-start': undefined
  'drag': { dx: number; dy: number }
  'close': undefined
  'minimize': undefined
}

/** 后端 → 前端响应消息（payload 不含 type 字段） */
export interface ResponseMap {
  'java-list': { javas: JavaEntry[] }
  'java-error': { message: string }
  'game-list': { path: string; games: GameEntry[] }
  'game-path-selected': { path: string }
  'account-list': { accounts: AccountEntry[] }
  'account-error': { message: string }
  /** 应用信息（版本号来自后端程序集，与 csproj Version 一致） */
  'app-info': { version: string }
  'game-error': { message: string }
  'game-launched': undefined
  'game-exited': undefined
  /** 启动阶段进度（stage: parse/java/run/waiting） */
  'launch-progress': { stage: string }
  /** 崩溃分析结果（中文解释，多行） */
  'crash-analysis': { message: string }
  /** 启动警告（如 Java 版本过低，不阻断启动） */
  'launch-warning': { message: string }
}
