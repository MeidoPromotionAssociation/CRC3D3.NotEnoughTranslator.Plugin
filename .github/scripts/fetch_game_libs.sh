#!/usr/bin/env bash
# Function: 把指定游戏版本的程序集拉进 lib/ / Download the game assemblies into lib/
#
# 本插件编译期需要 CRC 自带的程序集，它们不能提交进公开仓库，也不在公共 NuGet 源上
# （COM3D2.HighHeel 靠公开的 COM3D2.GameLibs 包，CRC3D3 还没有对应的包），
# 所以从私有仓库按游戏版本分目录下载：
#
#   https://github.com/MeidoPromotionAssociation/CRC3D3.GameLibs
#     ├── 1.0.1/   I2.Localization.dll, Assembly-CSharp.dll, Assembly-CSharp-firstpass.dll, WfSystem.FileSystem.dll, UnityEngine.UI.dll
#     └── demo2/    ...
#
# 切换游戏版本只需要改下面 DEFAULT_VERSION 这一行，或者用仓库变量 GAME_LIBS_VERSION 覆盖
# （变量优先级更高，不想改代码时用变量）。
#
# The plugin needs five assemblies shipped with the game. They must not be committed to
# this public repository and are on no public NuGet feed (COM3D2.HighHeel relies on the
# public COM3D2.GameLibs package, which has no CRC3D3 counterpart), so they are downloaded
# from a private repository that stores one directory per game version. Switch the game
# version by editing DEFAULT_VERSION below, or override it with the GAME_LIBS_VERSION
# repository variable (the variable wins, so no commit is needed).

set -euo pipefail

# ── 要编的游戏版本：对应私有仓库里的子目录名 ─────────────────────────────
# ── Game version to build against: the subdirectory name in the private repo ──
DEFAULT_VERSION="demo3"

# 私有仓库、分支和落盘位置，一般不用改 / Private repo, ref and destination, rarely changed
REPO="${GAME_LIBS_REPO:-MeidoPromotionAssociation/CRC3D3.GameLibs}"
REF="${GAME_LIBS_REF:-main}"
DEST_DIR="${DEST_DIR:-lib}"
VERSION="${GAME_LIBS_VERSION:-$DEFAULT_VERSION}"

FILES=(
  "I2.Localization.dll"
  "Assembly-CSharp.dll"
  "Assembly-CSharp-firstpass.dll"
  "WfSystem.FileSystem.dll"
  "UnityEngine.UI.dll"
)

if [ -z "${VERSION}" ]; then
  echo "::error::The game version is empty. Please set DEFAULT_VERSION or the repository variable GAME_LIBS_VERSION."
  exit 1
fi

if [ -z "${GH_TOKEN:-}" ]; then
  echo "::error::GAME_LIBS_TOKEN is not configured, and the game assembly cannot be pulled from the private repository ${REPO}."
  echo "::error::Please add GAME_LIBS_TOKEN (a PAT with contents:read permission for this private repository) in Settings → Secrets and variables → Actions."
  exit 1
fi

echo "game version: ${VERSION} (repo ${REPO}@${REF})"
mkdir -p "${DEST_DIR}"

for name in "${FILES[@]}"; do
  echo "fetching ${VERSION}/${name}"
  if ! gh api "repos/${REPO}/contents/${VERSION}/${name}?ref=${REF}" \
      -H "Accept: application/vnd.github.raw" > "${DEST_DIR}/${name}"; then
    echo "::error::failed to download ${VERSION}/${name} from ${REPO}@${REF}"
    echo "::error::Confirm that the private repository contains the directory ${VERSION}/, all five DLLs are present, and that GAME_LIBS_TOKEN has read permissions."
    exit 1
  fi

  # 校验：非空且带 PE 的 MZ 头。这样 404 之类返回的 JSON 不会被当成 dll 混进编译
  # Sanity check: non-empty and carrying the PE MZ header, so a JSON error page is not
  # mistaken for an assembly
  size="$(wc -c < "${DEST_DIR}/${name}")"
  magic="$(head -c 2 "${DEST_DIR}/${name}" || true)"
  if [ "${size}" -lt 1024 ] || [ "${magic}" != "MZ" ]; then
    echo "::error::${DEST_DIR}/${name} does not resemble a .NET assembly (${size} bytes, header ${magic})."
    exit 1
  fi
  echo "  ok, ${size} bytes"
done

# 记下这次编的是哪个游戏版本，方便对着 CI 日志和产物核对
# Record which game version this build used, so CI logs and artifacts can be matched up
echo "${VERSION}" > "${DEST_DIR}/GAME_LIBS_VERSION.txt"
echo "game libs ready: ${VERSION}"
ls -l "${DEST_DIR}"
