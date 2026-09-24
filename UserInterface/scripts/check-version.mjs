// 版本一致性守卫：package.json 的 version 必须与 ../DeciLauncher.csproj 的 <Version> 一致。
// 版本号已收敛为 csproj 单一来源（UserAgent/启动日志/关于页/CI 产物名均由此派生），
// 发版时若只改了其中一侧，CI 在此即时失败，防止 5 处硬编码漂移的老问题复发。
import { readFileSync } from 'node:fs'

const pkg = JSON.parse(readFileSync(new URL('../package.json', import.meta.url), 'utf8'))
const csproj = readFileSync(new URL('../../DeciLauncher.csproj', import.meta.url), 'utf8')
const csprojVersion = csproj.match(/<Version>([^<]+)<\/Version>/)?.[1]

if (!csprojVersion) {
  console.error('未在 DeciLauncher.csproj 中找到 <Version>')
  process.exit(1)
}
if (pkg.version !== csprojVersion) {
  console.error('版本不一致：package.json=' + pkg.version + '，csproj=' + csprojVersion)
  console.error('请同步修改两侧（csproj 为单一来源）')
  process.exit(1)
}
console.log('版本一致：' + csprojVersion)
