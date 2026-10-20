#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# Function: 组装发布 zip / Assemble the release zip
"""组装发布 zip。

解压到 CRC3D3 游戏根目录即可，包内布局与插件运行时读取的路径一致：

    BepInEx/plugins/CRC3D3.NotEnoughTranslator.Plugin.dll
    README.md
    Document/...                 （随包附带文档，README 里的相对链接才不会断）

**只打包插件本体这一个 dll，不做目录通配。**
`bin/Release/net48/` 里可能还躺着编译期引用的副本，游戏目录里本来就有（而且版本更匹配），
打进包里覆盖过去只会把游戏弄坏。

Extract into the CRC3D3 game root; the layout matches the paths the plugin reads at runtime.
Only the plugin dll is packaged, deliberately not a directory glob: the build output can
also contain copies of compile-time references, and overwriting the game's own copies would
break it.
"""

import argparse
import os
import sys
import zipfile

# 编译输出相对 --build-root 的位置、插件 dll 名、zip 前缀
# Build output location relative to --build-root, the plugin dll name and the zip prefix
BUILD_DIR = "net48"
PLUGIN_DLL = "CRC3D3.NotEnoughTranslator.Plugin.dll"
ZIP_PREFIX = "CRC3D3.NotEnoughTranslator.Plugin"

PLUGIN_DIR_IN_ZIP = "BepInEx/plugins"
DOCS_DIR = "Document"

# 固定时间戳，保证同样的输入产出同样的 zip / Fixed timestamp so identical inputs give identical zips
FIXED_DATE_TIME = (2020, 1, 1, 0, 0, 0)


def add_dir(zf, name):
    info = zipfile.ZipInfo(name.rstrip("/") + "/", date_time=FIXED_DATE_TIME)
    info.external_attr = (0o40755 << 16) | 0x10
    info.compress_type = zipfile.ZIP_STORED
    zf.writestr(info, b"")


def add_file(zf, src, arcname):
    info = zipfile.ZipInfo(arcname, date_time=FIXED_DATE_TIME)
    info.external_attr = 0o100644 << 16
    info.compress_type = zipfile.ZIP_DEFLATED
    with open(src, "rb") as f:
        zf.writestr(info, f.read(), compresslevel=9)


def collect_docs(repo_root):
    """收集 Document 下的文档，返回 [(绝对路径, 包内路径)]，按包内路径排序保证顺序稳定。
    Collect the docs under Document as [(abs path, archive path)], sorted for stable output."""
    docs_root = os.path.join(repo_root, DOCS_DIR)
    if not os.path.isdir(docs_root):
        return []
    items = []
    for dirpath, _, filenames in os.walk(docs_root):
        for filename in filenames:
            src = os.path.join(dirpath, filename)
            items.append((src, os.path.relpath(src, repo_root).replace(os.sep, "/")))
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
    readme = os.path.join(repo_root, "README.md")

    if not os.path.isfile(plugin_dll):
        found = sorted(os.listdir(build_dir)) if os.path.isdir(build_dir) else []
        sys.exit(f"::error::{PLUGIN_DLL} not found in {build_dir}, got {found}")
    if not os.path.isfile(readme):
        sys.exit(f"::error::missing file: {readme}")

    docs = collect_docs(repo_root)
    os.makedirs(os.path.join(repo_root, args.output_dir), exist_ok=True)
    zip_path = os.path.join(repo_root, args.output_dir, f"{ZIP_PREFIX}-{args.version}.zip")

    expected = {
        "BepInEx/",
        f"{PLUGIN_DIR_IN_ZIP}/",
        f"{PLUGIN_DIR_IN_ZIP}/{PLUGIN_DLL}",
        "README.md",
    }
    for _, arcname in docs:
        expected.add(arcname)

    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        add_dir(zf, "BepInEx")
        add_dir(zf, PLUGIN_DIR_IN_ZIP)
        add_file(zf, plugin_dll, f"{PLUGIN_DIR_IN_ZIP}/{PLUGIN_DLL}")
        add_file(zf, readme, "README.md")
        # 文档按目录建条目，保证解压后目录结构完整
        # Emit directory entries so the doc tree is complete after extraction
        emitted_dirs = set()
        for src, arcname in docs:
            parts = arcname.split("/")[:-1]
            for depth in range(1, len(parts) + 1):
                directory = "/".join(parts[:depth]) + "/"
                if directory not in emitted_dirs:
                    add_dir(zf, directory)
                    emitted_dirs.add(directory)
                    expected.add(directory)
            add_file(zf, src, arcname)

    # 自检：条目必须与预期完全一致，多一个少一个都算打包逻辑坏了
    # Sanity check: the entry list must match exactly, otherwise the packaging logic is broken
    with zipfile.ZipFile(zip_path) as zf:
        names = zf.namelist()
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
