#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# Function: 组装发布 zip / Assemble the release zip
"""组装发布 zip。

解压到 CRC3D3 游戏根目录即可，包内布局与插件运行时读取的路径一致：

    BepInEx/plugins/CRC3D3.NotEnoughTranslator.Plugin.dll
    BepInEx/NotEnoughTranslator/NET-README.md
    BepInEx/NotEnoughTranslator/NET-README.txt
    BepInEx/NotEnoughTranslator/Document文档/...

两个 README 和文档都放在插件自己的资源目录下，而不是游戏根目录：既不往游戏根目录丢文件，
也不会和别的插件的文件混在一起。

`Document文档` 这个目录名带中文是刻意的，资源管理器里一眼就能认出是文档。
zip 条目名统一按 UTF-8 编码并置位 UTF-8 标志，Windows 自带解压和 7-Zip 都能正确还原。

**只打包插件本体这一个 dll，不做目录通配。**
`bin/Release/net48/` 里可能还躺着编译期引用的副本，游戏目录里本来就有（而且版本更匹配），
打进包里覆盖过去只会把游戏弄坏。

Extract into the CRC3D3 game root; the layout matches the paths the plugin reads at runtime.
Both README files and the docs live inside the plugin's own resource folder instead of the game
root, so nothing is dropped at the game root and nothing collides with other plugins.
The CJK name of `Document文档` is deliberate, it is recognisable at a glance in Explorer.
Archive entry names are always UTF-8 with the UTF-8 flag set, which both Windows Explorer and
7-Zip decode correctly.
Only the plugin dll is packaged, deliberately not a directory glob: the build output can
also contain copies of compile-time references, and overwriting the game's own copies would
break it.
"""

import argparse
import os
import sys
import time
import zipfile

# 编译输出相对 --build-root 的位置、插件 dll 名、zip 前缀
# Build output location relative to --build-root, the plugin dll name and the zip prefix
BUILD_DIR = "net48"
PLUGIN_DLL = "CRC3D3.NotEnoughTranslator.Plugin.dll"
ZIP_PREFIX = "CRC3D3.NotEnoughTranslator.Plugin"

PLUGIN_DIR_IN_ZIP = "BepInEx/plugins"
# 插件自己的资源目录（游戏运行时也往这里读写翻译资源），README 与文档都放在它下面
# The plugin's own resource folder (where the runtime reads and writes resources);
# both README files and the docs live under it
RESOURCE_DIR_IN_ZIP = "BepInEx/NotEnoughTranslator"
DOCS_DIR_NAME = "Document文档"

# 仓库内的 README 源 → 包内路径：README.md 是完整说明，.github/NET-README.txt 是给人先看的简短版
# README sources in the repo → archive paths: README.md is the full manual, the one under
# .github is the short notice users read first
README_MD_SRC = "README.md"
README_MD_IN_ZIP = f"{RESOURCE_DIR_IN_ZIP}/NET-README.md"
README_TXT_SRC = os.path.join(".github", "NET-README.txt")
README_TXT_IN_ZIP = f"{RESOURCE_DIR_IN_ZIP}/NET-README.txt"

# 仓库根目录的文档源目录 → 包内目录
# Doc source directory at the repo root → destination directory inside the archive
DOCS_SRC_DIR = "Document"
DOCS_DIR_IN_ZIP = f"{RESOURCE_DIR_IN_ZIP}/{DOCS_DIR_NAME}"

# 包内 NET-README.md 与文档同级，所以要把 README 里指向仓库 `Document/` 的相对链接
# 重写成 `Document文档/`，否则解压后点进去是死链
# The in-archive NET-README.md sits next to the docs, so the relative `Document/` links have to
# be rewritten to `Document文档/`, otherwise they are dead after extraction
README_LINK_REWRITES = (("](Document/", f"]({DOCS_DIR_NAME}/"),)


def zip_date_time(path=None):
    """zip 条目时间戳：文件用自己的修改时间，目录用打包时刻。
    Timestamp for a zip entry: a file keeps its own modification time, a directory gets
    the packaging time."""
    stamp = os.path.getmtime(path) if path else time.time()
    local = time.localtime(stamp)
    # zip 的时间字段最早只能表示 1980 年 / the zip timestamp cannot represent years before 1980
    if local.tm_year < 1980:
        return (1980, 1, 1, 0, 0, 0)
    return local[:6]


def add_dir(zf, name):
    info = zipfile.ZipInfo(name.rstrip("/") + "/", date_time=zip_date_time())
    info.external_attr = (0o40755 << 16) | 0x10
    info.compress_type = zipfile.ZIP_STORED
    zf.writestr(info, b"")


def add_file(zf, src, arcname, text=False, rewrite=()):
    """写入一个文件。text=True 时统一换行成 CRLF，让 Windows 记事本正常显示；
    rewrite 是 [(旧串, 新串)]，用于修正包内相对链接。
    Add a file. With text=True the line endings are normalized to CRLF for the Windows notepad;
    rewrite is a [(old, new)] list used to fix relative links inside the archive."""
    if text:
        with open(src, encoding="utf-8", newline="") as f:
            body = f.read()
        for old, new in rewrite:
            body = body.replace(old, new)
        body = body.replace("\r\n", "\n").replace("\r", "\n").replace("\n", "\r\n").encode("utf-8")
    else:
        with open(src, "rb") as f:
            body = f.read()

    info = zipfile.ZipInfo(arcname, date_time=zip_date_time(src))
    info.external_attr = 0o100644 << 16
    info.compress_type = zipfile.ZIP_DEFLATED
    # 强制 UTF-8 条目名：`Document文档` 这类中文路径不能被当成 cp437 读
    # Force a UTF-8 entry name, so CJK paths such as `Document文档` are not read as cp437
    info.flag_bits |= 0x800
    zf.writestr(info, body, compresslevel=9)


def collect_docs(repo_root):
    """收集 Document 下的文档，返回 [(绝对路径, 相对 Document 的路径)]，排序保证顺序稳定。
    Collect the docs under Document as [(abs path, path relative to Document)], sorted for stable
    output."""
    docs_root = os.path.join(repo_root, DOCS_SRC_DIR)
    if not os.path.isdir(docs_root):
        return []
    items = []
    for dirpath, _, filenames in os.walk(docs_root):
        for filename in filenames:
            src = os.path.join(dirpath, filename)
            items.append((src, os.path.relpath(src, docs_root).replace(os.sep, "/")))
    return sorted(items, key=lambda item: item[1])


def main():
    parser = argparse.ArgumentParser(description="Assemble the release zip")
    parser.add_argument("--version", required=True, help="例如 0.0.1 / e.g. 0.0.1")
    parser.add_argument("--build-root", required=True,
                        help="dotnet build 的输出根目录（bin/Release）/ dotnet build output root")
    parser.add_argument("--repo-root", default=".", help="仓库根目录 / repository root")
    parser.add_argument("--output-dir", default="dist", help="zip 输出目录 / output directory")
    args = parser.parse_args()

    repo_root = os.path.abspath(args.repo_root)
    build_dir = os.path.join(os.path.abspath(args.build_root), BUILD_DIR)
    plugin_dll = os.path.join(build_dir, PLUGIN_DLL)
    readme_md = os.path.join(repo_root, README_MD_SRC)
    readme_txt = os.path.join(repo_root, README_TXT_SRC)

    if not os.path.isfile(plugin_dll):
        found = sorted(os.listdir(build_dir)) if os.path.isdir(build_dir) else []
        sys.exit(f"::error::{PLUGIN_DLL} not found in {build_dir}, got {found}")
    for path in (readme_md, readme_txt):
        if not os.path.isfile(path):
            sys.exit(f"::error::missing file: {path}")

    docs = collect_docs(repo_root)
    os.makedirs(os.path.join(repo_root, args.output_dir), exist_ok=True)
    zip_path = os.path.join(repo_root, args.output_dir, f"{ZIP_PREFIX}-{args.version}.zip")

    expected = {
        "BepInEx/",
        f"{PLUGIN_DIR_IN_ZIP}/",
        f"{PLUGIN_DIR_IN_ZIP}/{PLUGIN_DLL}",
        f"{RESOURCE_DIR_IN_ZIP}/",
        README_MD_IN_ZIP,
        README_TXT_IN_ZIP,
    }
    if docs:
        expected.add(f"{DOCS_DIR_IN_ZIP}/")
    for _, rel in docs:
        expected.add(f"{DOCS_DIR_IN_ZIP}/{rel}")

    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        # 目录条目统一在这里去重，避免同一路径写两次触发 Duplicate name 警告
        # Directory entries are deduplicated here, so no path is written twice
        emitted_dirs = set()

        def emit_dir(name):
            directory = name.rstrip("/") + "/"
            if directory not in emitted_dirs:
                add_dir(zf, directory)
                emitted_dirs.add(directory)
                expected.add(directory)

        emit_dir("BepInEx")
        emit_dir(PLUGIN_DIR_IN_ZIP)
        add_file(zf, plugin_dll, f"{PLUGIN_DIR_IN_ZIP}/{PLUGIN_DLL}")
        emit_dir(RESOURCE_DIR_IN_ZIP)
        add_file(zf, readme_md, README_MD_IN_ZIP, text=True, rewrite=README_LINK_REWRITES)
        add_file(zf, readme_txt, README_TXT_IN_ZIP, text=True)
        # 文档按目录建条目，保证解压后目录结构完整
        # Emit directory entries so the doc tree is complete after extraction
        for src, rel in docs:
            parts = f"{DOCS_DIR_IN_ZIP}/{rel}".split("/")[:-1]
            for depth in range(1, len(parts) + 1):
                emit_dir("/".join(parts[:depth]))
            add_file(zf, src, f"{DOCS_DIR_IN_ZIP}/{rel}")

    # 自检：条目必须与预期完全一致，多一个少一个都算打包逻辑坏了
    # Sanity check: the entry list must match exactly, otherwise the packaging logic is broken
    with zipfile.ZipFile(zip_path) as zf:
        names = zf.namelist()
    # 中文目录名必须能按 UTF-8 读回来，不能被当成 cp437
    # The CJK directory name must decode as UTF-8 rather than being read as cp437
    for name in names:
        if "\ufffd" in name:
            sys.exit(f"::error::entry name is not valid UTF-8 in {zip_path}: {name!r}")
    unexpected = sorted(set(names) - expected)
    missing = sorted(expected - set(names))
    if unexpected:
        sys.exit(f"::error::unexpected entries in {zip_path}: {unexpected}")
    if missing:
        sys.exit(f"::error::missing entries in {zip_path}: {missing}")

    size = os.path.getsize(zip_path)
    print(f"built {zip_path} ({len(names)} entries, {size} bytes)")


if __name__ == "__main__":
    main()