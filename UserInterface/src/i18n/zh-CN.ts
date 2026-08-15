// 中文（简体）语言包 — 键名语义化，值与当前界面文案保持一致
export const zhCN: Record<string, string> = {
  // 通用
  'common.cancel': '取消',
  'common.refresh': '刷新',
  'common.refreshing': '刷新中...',
  'common.underConstruction': '开发中...',

  // 标题栏
  'titlebar.localStorageReset': 'localStorage 重置成功',

  // 导航
  'nav.home': '主页',
  'nav.games': '游戏',
  'nav.downloads': '下载',
  'nav.accounts': '账户',
  'nav.settings': '设置',

  // 主页
  'home.noInstanceSelected': '未选择实例',
  'home.launching': '启动中...',
  'home.closeInstance': '关闭游戏',
  'home.launchInstance': '启动游戏',
  'home.stage.parse': '正在解析游戏版本...',
  'home.stage.java': '正在查找 Java 运行时...',
  'home.stage.run': '正在启动游戏...',
  'home.stage.waiting': '正在等待游戏窗口...',

  // 游戏页
  'games.directory': '实例目录：{path}',
  'games.browse': '浏览',
  'games.vanilla': '原版',
  'games.modded': '可安装模组',
  'games.minecraft': 'Minecraft {version}',
  'games.noneFound': '未找到已安装的实例',
  'games.switchLocked': '无法在游戏启动或运行期间切换实例',

  // 账户页
  'accounts.createOffline': '创建离线账户',
  'accounts.loginMicrosoft': '登录正版账户',
  'accounts.loginThirdParty': '登录第三方账户',
  'accounts.noneFound': '还没有账户',
  'accounts.playerName': '玩家名',
  'accounts.create': '创建',
  'accounts.typeOffline': '离线账户',
  'accounts.typeMicrosoft': '正版账户',
  'accounts.typeYggdrasil': '第三方账户',
  'accounts.copied': '已复制到剪贴板',

  // 设置页
  'settings.instanceSettings': '游戏设置',
  'settings.launcherSettings': '启动器设置',
  'settings.about': '关于',
  'settings.javaRuntime': 'Java 运行时',
  'settings.scan': '扫描',
  'settings.scanning': '扫描中...',
  'settings.autoSelect': '自动选择',
  'settings.noJavaFound': '没有找到 Java 运行时',
  'settings.notScannedYet': '还没有扫描哦~',
  'settings.unknown': 'Unknown',
  'settings.instanceMemory': '游戏内存',
  'settings.gb': 'GB',
  'settings.mb': 'MB',
  'settings.downloadSource': '下载源',
  'settings.downloadSourceMirror': '尽量使用镜像源',
  'settings.downloadSourceOfficialFirst': '优先使用官方源，在加载缓慢时换用镜像源',
  'settings.downloadSourceOfficial': '尽量使用官方源',
  'settings.language': '语言',
  'settings.langZhCN': '简体中文',
  'settings.langEnUS': 'English',

  // 关于页
  'about.version': 'v{version}',
  'about.author': '作者：Tristan Jen',
  'about.company': '公司：Decibyte',
  'about.license': '许可证：GPL-3.0',
  'about.thanksTitle': '开源项目'
}
