#!/bin/bash
# 构建 macOS DMG：.app + /Applications 快捷方式，并用 Finder 布局居中两个图标。
# 用法: make-dmg.sh <app 路径> <输出 .dmg> [卷名]
# 说明: Finder 布局需要当前用户有 Finder 自动化权限；无 GUI 会话时布局会被跳过，
#       DMG 中仍包含 .app 与 Applications 快捷方式。

set -euo pipefail

APP="${1:?用法: make-dmg.sh <app 路径> <输出 .dmg> [卷名]}"
OUT="${2:?用法: make-dmg.sh <app 路径> <输出 .dmg> [卷名]}"
VOLUME="${3:-Deci Launcher}"
APP_NAME="$(basename "$APP")"

if [ ! -d "$APP" ]; then
    echo "找不到 .app: $APP" >&2
    exit 1
fi
if [ -d "/Volumes/$VOLUME" ]; then
    echo "卷 \"$VOLUME\" 已挂载，请先卸载后重试。" >&2
    exit 1
fi

WORK="$(mktemp -d)"
STAGE="$WORK/stage"
RW="$WORK/DeciLauncher-rw.dmg"
MOUNTED=0

cleanup() {
    if [ "$MOUNTED" -eq 1 ]; then
        hdiutil detach "/Volumes/$VOLUME" -quiet 2>/dev/null || true
    fi
    rm -rf "$WORK"
}
trap cleanup EXIT

mkdir -p "$STAGE"
ditto "$APP" "$STAGE/$APP_NAME"
ln -s /Applications "$STAGE/Applications"

# 先用可读写镜像写入 Finder 布局，再转换为压缩镜像
hdiutil create -volname "$VOLUME" -srcfolder "$STAGE" -ov -format UDRW -fs HFS+ "$RW" >/dev/null
hdiutil attach -nobrowse "$RW" >/dev/null
MOUNTED=1

# Finder 布局：600x320 窗口，128px 图标，两个图标作为一组居中
# （坐标相对内容视图左上角；图标 position 是图标中心，标签在下方，
#  故 y 需比内容中心略小，按「图标 + 标签」整体居中）
perl -e 'alarm 30; exec @ARGV' osascript <<EOF >/dev/null 2>&1 || true
tell application "Finder"
  tell disk "$VOLUME"
    open
    set current view of container window to icon view
    set toolbar visible of container window to false
    set statusbar visible of container window to false
    set the bounds of container window to {240, 240, 840, 560}
    set viewOptions to the icon view options of container window
    set arrangement of viewOptions to not arranged
    set icon size of viewOptions to 128
    set position of item "$APP_NAME" of container window to {200, 127}
    set position of item "Applications" of container window to {400, 127}
    close
    open
    update without registering applications
    delay 1
  end tell
end tell
EOF

sync
hdiutil detach "/Volumes/$VOLUME" -quiet
MOUNTED=0

rm -f "$OUT"
hdiutil convert "$RW" -format UDZO -o "$OUT" >/dev/null
echo "已生成: $OUT"
