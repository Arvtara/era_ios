#!/usr/bin/env bash
# ============================================================
# uEmuera iOS 一键构建脚本（macOS / 黑苹果）
#
# 前置条件：
#   1. Unity(iOS Build Support) 已导出 Xcode 工程到本目录
#      （工程根含 Unity-iPhone.xcworkspace）
#   2. Xcode 14.x、brew install ldid
#
# 用法：
#   ./build_ipa.sh
#
# 产物：dist/uEmuera.tipa  → AirDrop 到设备 → TrollStore 安装
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="${1:-$SCRIPT_DIR}"
BUILD_DIR="$SRC_DIR/build"
DIST_DIR="$SCRIPT_DIR/dist"
ENTITLEMENTS="$SCRIPT_DIR/entitlements.plist"

cd "$SRC_DIR"

if [ ! -d "Unity-iPhone.xcworkspace" ]; then
    echo "错误：未找到 Unity-iPhone.xcworkspace，请先在 Unity 中导出 Xcode 工程" >&2
    exit 1
fi

echo "==> [1/4] xcodebuild（无签名构建，无需开发者账号）"
xcodebuild -workspace Unity-iPhone.xcworkspace -scheme Unity-iPhone \
    -configuration Release -sdk iphoneos -destination 'generic/platform=iOS' \
    CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" \
    -derivedDataPath build

APP_PATH=$(find "$BUILD_DIR/Build/Products/Release-iphoneos" -maxdepth 1 -name "*.app" | head -n 1)
if [ -z "$APP_PATH" ]; then
    echo "错误：未找到构建产物 .app" >&2
    exit 1
fi
APP_NAME=$(basename "$APP_PATH" .app)
echo "    产物: $APP_PATH"

echo "==> [2/4] 组装 Payload"
rm -rf "$DIST_DIR"
mkdir -p "$DIST_DIR/Payload"
cp -R "$APP_PATH" "$DIST_DIR/Payload/"

echo "==> [3/4] 注入 TrollStore entitlements（若 ldid 不存在则跳过，App 将运行于纯沙盒模式）"
if command -v ldid >/dev/null 2>&1; then
    ldid -S"$ENTITLEMENTS" "$DIST_DIR/Payload/$APP_NAME/$APP_NAME"
    echo "    已注入: no-sandbox + platform-application"
else
    echo "    警告: 未安装 ldid（brew install ldid），跳过 entitlements 注入"
fi

echo "==> [4/4] 打包 tipa"
cd "$DIST_DIR"
zip -qry "$APP_NAME.tipa" Payload
cd "$SCRIPT_DIR"
rm -rf "$DIST_DIR/Payload"

echo ""
echo "完成: $DIST_DIR/$APP_NAME.tipa"
echo "下一步: AirDrop / 文件 App 传到设备 → 分享给 TrollStore → 安装"
