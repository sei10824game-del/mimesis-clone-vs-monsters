#!/bin/bash
set -e
# CloneVsMonsters ビルドスクリプト

# 使用方法:
#   ./build.sh                                    # 環境変数 MIMESIS_PATH または自動検出
#   ./build.sh /path/to/MIMESIS                   # GamePath を直接指定
#   ./build.sh --stub                             # ゲームなしでスタブビルド（CI用）

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$SCRIPT_DIR/CloneVsMonsters/CloneVsMonsters.csproj"

if [ "$1" = "--stub" ]; then
    echo "=== Stub build (no game required) ==="
    dotnet build "$PROJECT" -c Release -p:SkipGameRefs=true
    echo "=== Done: bin/Release/netstandard2.1/CloneVsMonsters.dll ==="
    exit 0
fi

GAME_PATH="${1:-$MIMESIS_PATH}"

if [ -n "$GAME_PATH" ]; then
    echo "=== Building with GamePath: $GAME_PATH ==="
    dotnet build "$PROJECT" -c Release -p:GamePath="$GAME_PATH"
else
    echo "=== Building with auto-detected GamePath ==="
    dotnet build "$PROJECT" -c Release
fi

echo "=== Done ==="
echo "Output: CloneVsMonsters/bin/Release/netstandard2.1/CloneVsMonsters.dll"
echo "Copy to: \$MIMESIS/Mods/CloneVsMonsters.dll"
