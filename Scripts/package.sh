#!/usr/bin/env bash
# ============================================================
#  Godot 版本管理器 - 打包脚本（macOS / Linux）
#
#  用法:
#    ./Scripts/package.sh                 按当前系统自动选择运行时标识打包
#    ./Scripts/package.sh -r osx-arm64    指定运行时标识
#    ./Scripts/package.sh -v 0.3.0        指定版本号（默认取 csproj 的 <Version>）
#    ./Scripts/package.sh -o ./out        指定输出目录（默认 <仓库>/dist）
#    ./Scripts/package.sh -f              框架依赖版（目标机器需自行安装 .NET 10 运行时）
#    ./Scripts/package.sh --no-zip        只发布，不压缩
#    ./Scripts/package.sh --no-clean      不清空旧的发布目录
#    ./Scripts/package.sh --setup         额外生成 Windows 自解压安装包
#    ./Scripts/package.sh -h              查看帮助
#
#  产物（都在 dist 下）:
#    dist/GodotManager-<版本>-<rid>/       发布目录（exe + 原生依赖 DLL，整个目录一起分发）
#    dist/GodotManager-<版本>-<rid>.zip    压缩包
#    dist/GodotManager-<版本>-win-x64-setup.exe  自解压安装包（--setup）
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJ="$ROOT/GodotManager.csproj"
PKG_OUTDIR="$ROOT/dist"
PKG_VERSION=""
SELF_CONTAINED="true"
MAKE_ZIP="true"
DO_CLEAN="true"
SETUP="false"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

detect_rid() {
    local os arch
    os="$(uname -s)"
    arch="$(uname -m)"
    case "$os" in
        Darwin)
            if [ "$arch" = "arm64" ] || [ "$arch" = "aarch64" ]; then
                echo "osx-arm64"
            else
                echo "osx-x64"
            fi
            ;;
        Linux)
            if [ "$arch" = "arm64" ] || [ "$arch" = "aarch64" ]; then
                echo "linux-arm64"
            else
                echo "linux-x64"
            fi
            ;;
        *)
            echo "linux-x64"
            ;;
    esac
}

RID="$(detect_rid)"

usage() {
    cat <<'EOF'
用法: ./Scripts/package.sh [选项]

  -r, --rid <rid>           运行时标识，默认按当前系统自动选择
  -v, --version <版本>      版本号，默认读取 GodotManager.csproj 的 <Version>
  -o, --output <目录>       输出目录，默认 <仓库>/dist
  -f, --framework-dependent 框架依赖发布（目标机器需自行安装 .NET 10 运行时）
  --no-zip                 只发布，不压缩
  --no-clean               不清空旧的发布目录
  --setup                  额外生成 Windows 自解压安装包（单文件 exe）
  -h, --help               显示本帮助

示例:
  ./Scripts/package.sh
  ./Scripts/package.sh -v 0.3.0 -r win-x64
  ./Scripts/package.sh -f --no-zip
EOF
}

need_value() {
    if [ -z "${2:-}" ]; then
        echo "[错误] $1 需要一个参数" >&2
        exit 1
    fi
}

while [ $# -gt 0 ]; do
    case "$1" in
        -h|--help)
            usage
            exit 0
            ;;
        -v|--version)
            need_value "$1" "${2:-}"
            PKG_VERSION="$2"
            shift 2
            ;;
        -r|--rid)
            need_value "$1" "${2:-}"
            RID="$2"
            shift 2
            ;;
        -o|--output)
            need_value "$1" "${2:-}"
            PKG_OUTDIR="$2"
            shift 2
            ;;
        -f|--framework-dependent)
            SELF_CONTAINED="false"
            shift
            ;;
        --no-zip)
            MAKE_ZIP="false"
            shift
            ;;
        --no-clean)
            DO_CLEAN="false"
            shift
            ;;
        --setup)
            SETUP="true"
            shift
            ;;
        *)
            echo "[错误] 无法识别的参数: $1" >&2
            echo >&2
            usage
            exit 1
            ;;
    esac
done

if [ ! -f "$PROJ" ]; then
    echo "[错误] 找不到项目文件: $PROJ" >&2
    exit 1
fi

if ! command -v "$DOTNET_BIN" >/dev/null 2>&1; then
    echo "[错误] 未找到 dotnet，请先安装 .NET 10 SDK 并加入 PATH。" >&2
    exit 1
fi

if [ -z "$PKG_VERSION" ]; then
    PKG_VERSION="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$PROJ" | head -n 1 | tr -d '[:space:]')"
fi

if [ -z "$PKG_VERSION" ]; then
    echo "[错误] 无法从 $PROJ 读取版本号，请用 -v 手动指定。" >&2
    exit 1
fi

PUBDIR="$PKG_OUTDIR/GodotManager-$PKG_VERSION-$RID"
ZIP="$PKG_OUTDIR/GodotManager-$PKG_VERSION-$RID.zip"

echo
echo "============================================"
echo " Godot 版本管理器 打包"
echo "============================================"
echo "  版本          : $PKG_VERSION"
echo "  运行时标识    : $RID"
echo "  发布模式      : $SELF_CONTAINED"
echo "  发布目录      : $PUBDIR"
if [ "$MAKE_ZIP" = "true" ]; then
    echo "  压缩包        : $ZIP"
fi
echo

if [ "$DO_CLEAN" = "true" ] && [ -d "$PUBDIR" ]; then
    rm -rf "$PUBDIR"
fi
mkdir -p "$PUBDIR" "$PKG_OUTDIR"

echo "[1/3] dotnet publish ..."
"$DOTNET_BIN" publish "$PROJ" -c Release -r "$RID" --self-contained "$SELF_CONTAINED" -p:PublishSingleFile=true -p:DebugType=none -p:PublishTrimmed=false -o "$PUBDIR" --nologo

echo "[2/3] 复制说明文件 ..."
if [ -f "$ROOT/readme.md" ]; then
    cp -f "$ROOT/readme.md" "$PUBDIR/README.md"
fi
# 运行时包自带的 pdb 体积很大，不随包分发
rm -f "$PUBDIR"/*.pdb

if [ "$MAKE_ZIP" = "true" ]; then
    echo "[3/3] 压缩 ..."
    rm -f "$ZIP"
    if command -v zip >/dev/null 2>&1; then
        (cd "$PUBDIR" && zip -qr "$ZIP" .)
    else
        echo "      zip 不可用，改用 tar.gz ..."
        ZIP="${ZIP%.zip}.tar.gz"
        rm -f "$ZIP"
        (cd "$PUBDIR" && tar -czf "$ZIP" .)
    fi
else
    echo "[3/3] 跳过压缩。"
fi

if [ "$SETUP" = "true" ]; then
    echo "[4/4] 生成自解压安装包 ..."
    LOADDIR="$PKG_OUTDIR/setup-payload"
    PAYLOAD="$ROOT/Tools/Setup/Payload.zip"
    SETUPEXE="$PKG_OUTDIR/GodotManager-$PKG_VERSION-win-x64-setup.exe"
    rm -rf "$LOADDIR"
    mkdir -p "$LOADDIR"
    cp -r "$PUBDIR"/. "$LOADDIR"/
    cp -f "$ROOT/Scripts/install.ps1" "$LOADDIR"/
    cp -f "$ROOT/Scripts/uninstall.ps1" "$LOADDIR"/
    printf '%s\n' "$PKG_VERSION" > "$LOADDIR/version.txt"
    rm -f "$PAYLOAD"
    if command -v zip >/dev/null 2>&1; then
        (cd "$LOADDIR" && zip -qr "$PAYLOAD" .)
    elif command -v 7z >/dev/null 2>&1; then
        (cd "$LOADDIR" && 7z a -tzip "$PAYLOAD" . >/dev/null)
    elif command -v python3 >/dev/null 2>&1; then
        python3 -c "import os,sys,zipfile; d=sys.argv[1]; z=zipfile.ZipFile(sys.argv[2],'w',zipfile.ZIP_DEFLATED); [z.write(os.path.join(d,f),f) for f in os.listdir(d) if os.path.isfile(os.path.join(d,f))]; z.close()" "$LOADDIR" "$PAYLOAD"
    else
        echo "      找不到可用的 zip 工具，跳过安装包。"
        rm -rf "$LOADDIR"
        exit 0
    fi
    rm -rf "$PKG_OUTDIR/setup-build"
    "$DOTNET_BIN" publish "$ROOT/Tools/Setup/Setup.csproj" -c Release -r win-x64 -o "$PKG_OUTDIR/setup-build" --nologo
    cp -f "$PKG_OUTDIR/setup-build/GodotManager-Setup.exe" "$SETUPEXE"
    rm -rf "$LOADDIR" "$PKG_OUTDIR/setup-build" "$PAYLOAD"
    echo "  安装包: $SETUPEXE"
fi

echo
echo "打包完成。"
echo "  发布目录: $PUBDIR"
if [ "$MAKE_ZIP" = "true" ]; then
    ls -lh "$ZIP" | awk '{print "  压缩包  : " $9 " (" $5 ")"}'
fi
echo "  注意: Avalonia 的原生依赖（libSkiaSharp.dll 等）不会被合并进 exe，需整个目录一起分发。"
echo