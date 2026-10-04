#!/usr/bin/env bash
# ============================================================
# 把导出的 Xcode 工程推送到 GitHub（供云端构建）
#
# 用法：
#   cd <导出的Xcode工程目录>
#   把本脚本、.gitignore、entitlements.plist、
#   .github/workflows/build-ios.yml 一起放进来
#   ./push_to_github.sh <你的仓库地址>
#
# 例：
#   ./push_to_github.sh git@github.com:yourname/uemuera-ios.git
# ============================================================
set -euo pipefail

REPO_URL="${1:-}"
if [ -z "$REPO_URL" ]; then
    echo "用法: $0 <git仓库地址>" >&2
    echo "  https://github.com/你的用户名/uemuera-ios.git" >&2
    echo "  git@github.com:你的用户名/uemuera-ios.git" >&2
    exit 1
fi

echo "==> [1/5] 检查超大文件（GitHub 单文件上限 100MB）"
BIG=$(find . -type f -size +95M -not -path "./.git/*" 2>/dev/null || true)
if [ -n "$BIG" ]; then
    echo "以下文件超过 95MB，会推送失败，必须用 Git LFS 或剔除："
    echo "$BIG" | while read -r f; do
        printf "  %8s  %s\n" "$(du -h "$f" | cut -f1)" "$f"
    done
    echo ""
    echo "处理方式（二选一）："
    echo "  A) 用 LFS:  git lfs track \"$(echo "$BIG" | head -1)\"  （逐个 track 后重新 add）"
    echo "  B) 确认这些文件不是构建必需（如 .a 静态库可由 Xcode 重新生成）则删除"
    echo ""
    read -r -p "已处理完毕，继续？[y/N] " ans
    [ "$ans" = "y" ] || [ "$ans" = "Y" ] || exit 1
fi

echo "==> [2/5] 初始化仓库"
if [ ! -d .git ]; then
    git init
    git branch -M main
fi
git remote remove origin 2>/dev/null || true
git remote add origin "$REPO_URL"

echo "==> [3/5] 暂存文件"
git add -A
STAGED=$(git diff --cached --numstat | wc -l)
echo "    待提交文件数: $STAGED"
git diff --cached --stat | tail -3

echo "==> [4/5] 提交"
git commit -m "uEmuera iOS: Unity-exported Xcode project for cloud build" || echo "    无新变更"

echo "==> [5/5] 推送（875MB 级别首次推送可能需要几分钟）"
git push -u origin main

echo ""
echo "完成。下一步："
echo "  1. 打开 GitHub 仓库 → Actions 标签页"
echo "  2. 选择 'Build iOS IPA' 工作流 → Run workflow"
echo "  3. 等待约 15-30 分钟，在 Artifacts 下载 uEmuera-tipa"
