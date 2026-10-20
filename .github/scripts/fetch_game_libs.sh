#!/usr/bin/env bash
# Function: 把指定游戏版本的三个程序集拉进 lib/ / Download the game assemblies into lib/
#
# 本插件编译期需要 CRC 自带的三个程序集，它们不能提交进公开仓库，也不在公共 NuGet 源上
# （COM3D2.HighHeel 靠公开的 COM3D2.GameLibs 包，CRC3D3 还没有对应的包），
# 所以从私有仓库按游戏版本分目录下载：
#
#   https://github.com/MeidoPromotionAssociation/CRC3D3.GameLibs
#     ├── 2.49.2/   I2.Localization.dll, Assembly-CSharp.dll, Assembly-CSharp-firstpass.dll
#     └── demo2/    ...
#
# 切换游戏版本只需要改下面 DEFAULT_VERSION 这一行，或者用仓库变量 GAME_LIBS_VERSION 覆盖
# （变量优先级更高，不想改代码时用变量）。
#
# The plugin needs three assemblies shipped with the game. They must not be committed to
# this public repository and are on no public NuGet feed (COM3D2.HighHeel relies on the
# public COM3D2.GameLibs package, which has no CRC3D3 counterpart), so they are downloaded
# from a private repository that stores one directory per game version. Switch the game
# version by editing DEFAULT_VERSION below, or override it with the GAME_LIBS_VERSION
# repository variable (the variable wins, so no commit is needed).

set -euo pipefail

# ── 要编的游戏版本：对应私有仓库里的子目录名 ─────────────────────────────
# ── Game version to build against: the subdirectory name in the private repo ──
DEFAULT_VERSION="demo2"

# 私有仓库、分支和落盘位置，一般不用改 / Private repo, ref and destination, rarely changed
REPO="${GAME_LIBS_REPO:-MeidoPromotionAssociation/CRC3D3.GameLibs}"
REF="${GAME_LIBS_REF:-main}"
DEST_DIR="${DEST_DIR:-lib}"
VERSION="${GAME_LIBS_VERSION:-$DEFAULT_VERSION}"

FILES=(
  "I2.Localization.dll"
  "Assembly-CSharp.dll"
  "Assembly-CSharp-firstpass.dll"
)

if [ -z "${VERSION}" ]; then
  echo "::error::游戏版本为空，请设置 DEFAULT_VERSION 或仓库变量 GAME_LIBS_VERSION / game version is empty"
  exit 1
fi

if [ -z "${GH_TOKEN:-}" ]; then
  echo "::error::GAME_LIBS_TOKEN 未配置，无法从私有仓库 ${REPO} 拉取游戏程序集。/ GAME_LIBS_TOKEN is not set."
  echo "::error::请在仓库 Settings → Secrets and variables → Actions 里添加 GAME_LIBS_TOKEN（对该私有仓库有 contents:read 权限的 PAT）。"
  exit 1
fi

echo "game version: ${VERSION} (repo ${REPO}@${REF})"
mkdir -p "${DEST_DIR}"

for name in "${FILES[@]}"; do
  echo "fetching ${VERSION}/${name}"
  if ! gh api "repos/${REPO}/contents/${VERSION}/${name}?ref=${REF}" \
      -H "Accept: application/vnd.github.raw" > "${DEST_DIR}/${name}"; then
    echo "::error::failed to download ${VERSION}/${name} from ${REPO}@${REF}"
    echo "::error::确认私有仓库里有 ${VERSION}/ 这个目录、三个 dll 齐全，且 GAME_LIBS_TOKEN 有读权限。/ Check the directory, the three files and the token scope."
    exit 1
  fi

  # 校验：非空且带 PE 的 MZ 头。这样 404 之类返回的 JSON 不会被当成 dll 混进编译
  # Sanity check: non-empty and carrying the PE MZ header, so a JSON error page is not
  # mistaken for an assembly
  size="$(wc -c < "${DEST_DIR}/${name}")"
  magic="$(head -c 2 "${DEST_DIR}/${name}" || true)"
  if [ "${size}" -lt 1024 ] || [ "${magic}" != "MZ" ]; then
    echo "::error::${DEST_DIR}/${name} 不像一个 .NET 程序集（${size} 字节，头 ${magic}）/ not a .NET assembly"
    exit 1
  fi
  echo "  ok, ${size} bytes"
done

# 记下这次编的是哪个游戏版本，方便对着 CI 日志和产物核对
# Record which game version this build used, so CI logs and artifacts can be matched up
echo "${VERSION}" > "${DEST_DIR}/GAME_LIBS_VERSION.txt"
echo "game libs ready: ${VERSION}"
ls -l "${DEST_DIR}"
