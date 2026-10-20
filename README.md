[English](#english) | [简体中文](#简体中文)

# CRC3D3.NotEnoughTranslator.Plugin

## English

NotEnoughTranslator (NET) is a BepInEx 5 translation plugin for CRC3D3.

**Status:** foundation stage. The I2 query hook is implemented; general text and texture replacement currently provide resource loading and query APIs only. Their game display/resource hooks are not connected yet. Translation data is not included.

NET keeps the game's language state unchanged. It does not add languages or provide JAT-style translation-language selection.

Under `BepInEx/NotEnoughTranslator`, resources follow JAT's layout without the language layer: `UI/Text`, `Text`, and `Texture`. Opt-in dump infrastructure writes to the matching subdirectories under `Dump`, which are never loaded as translations.

Optional XUAT interop uses JAT-style translation decisions, without adding or detecting special markers. See the [interop boundaries](Document/English/TranslationGuide.md#8-xuat-interoperability).

### Documentation

- [Modules and architecture](Document/English/ModuleArchitecture.md): module boundaries, I2 behavior, lifecycle, integration APIs, and building.
- [Translation and resource guide](Document/English/TranslationGuide.md): directories, CSV / JSONL formats, regex rules, textures, and reloading.

### Building

Target: .NET Framework 4.8 / Unity 2022.3 / BepInEx 5. The build needs three assemblies shipped with CRC3D3, placed in `lib` at the repository root: `I2.Localization.dll`, `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`.

```
dotnet build CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin.csproj -c Release
```

Output: `CRC3D3.NotEnoughTranslator.Plugin/bin/Release/net48/CRC3D3.NotEnoughTranslator.Plugin.dll`.

`lib` is git-ignored. CI downloads the same three files from the private repository [`MeidoPromotionAssociation/CRC3D3.GameLibs`](https://github.com/MeidoPromotionAssociation/CRC3D3.GameLibs), which keeps one directory per game version (`2.49.2/`, `demo2/`, ...), using the `GAME_LIBS_TOKEN` secret. The version is the `DEFAULT_VERSION` constant at the top of [fetch_game_libs.sh](.github/scripts/fetch_game_libs.sh); the `GAME_LIBS_VERSION` repository variable overrides it without a commit, and `GAME_LIBS_REPO` / `GAME_LIBS_REF` override the repository and ref.

### Releasing

1. Bump `PluginVersion` in [CRC3D3.NotEnoughTranslator.Plugin.cs](CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin.cs). The assembly version is derived from that constant.
2. Push a matching tag: `git tag v0.0.2 && git push origin v0.0.2`.

The `Release` workflow then builds, packages `CRC3D3.NotEnoughTranslator.Plugin-<version>.zip` (dll, README and `Document/`) and publishes the release. A tag that does not match `PluginVersion` fails the build instead of shipping a wrong version, and a tag containing a hyphen (`v0.1.0-rc1`) is published as a prerelease. Hand-written notes at `.github/release-notes/<tag>.md` take precedence over the generated template.

## 简体中文

NotEnoughTranslator（NET）是 CRC3D3 的 BepInEx 5 翻译插件。

**当前状态：基础骨架阶段。** 已实现 I2 查询 Hook；通用文本和纹理替换目前仅提供资源加载与查询接口，尚未挂接具体游戏显示或资源入口。本插件不附带翻译数据。

NET 保持游戏原有语言状态，不新增语言，也不提供 JAT 式的译文语言选择。

资源沿用 JAT 去掉语言层后的目录：`BepInEx/NotEnoughTranslator` 下的 `UI/Text`、`Text`、`Texture`。可选的导出基础设施写入 `Dump` 下的对应子目录，导出文件不会被当作翻译加载。

可选的 XUAT 互操作采用 JAT 式翻译判定，不添加或检测特殊标记。具体能力边界见 [XUAT 互操作说明](Document/简体中文_SimplifiedChinese/翻译指南.md#8-xuat-互操作)。

### 文档

- [模块架构](Document/简体中文_SimplifiedChinese/模块架构.md)：模块边界、I2 行为、生命周期、接入接口与构建。
- [翻译与资源指南](Document/简体中文_SimplifiedChinese/翻译指南.md)：目录、CSV / JSONL 格式、正则规则、纹理与重载。

### 构建

目标环境：.NET Framework 4.8 / Unity 2022.3 / BepInEx 5。编译需要 CRC3D3 自带的三个程序集，放在仓库根目录的 `lib` 下：`I2.Localization.dll`、`Assembly-CSharp.dll`、`Assembly-CSharp-firstpass.dll`。

```
dotnet build CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin.csproj -c Release
```

输出：`CRC3D3.NotEnoughTranslator.Plugin/bin/Release/net48/CRC3D3.NotEnoughTranslator.Plugin.dll`。

`lib` 已被 git 忽略。CI 用 `GAME_LIBS_TOKEN` 这个 secret 从私有仓库 [`MeidoPromotionAssociation/CRC3D3.GameLibs`](https://github.com/MeidoPromotionAssociation/CRC3D3.GameLibs) 拉取同样三个文件，该仓库按游戏版本分目录（`2.49.2/`、`demo2/` 之类）。要编哪个版本看 [fetch_game_libs.sh](.github/scripts/fetch_game_libs.sh) 顶部的 `DEFAULT_VERSION`，改一行即可；不想改代码时用仓库变量 `GAME_LIBS_VERSION` 覆盖，`GAME_LIBS_REPO` / `GAME_LIBS_REF` 分别覆盖仓库和分支。

### 发布

1. 修改 [CRC3D3.NotEnoughTranslator.Plugin.cs](CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin.cs) 里的 `PluginVersion`，程序集版本由该常量推导。
2. 推送同名 tag：`git tag v0.0.2 && git push origin v0.0.2`。

`Release` workflow 会编译、打包 `CRC3D3.NotEnoughTranslator.Plugin-<版本>.zip`（dll、README 与 `Document/`）并发布 release。tag 与 `PluginVersion` 不一致时构建直接失败，不会发出错版本的包；带连字符的 tag（`v0.1.0-rc1`）按预发布处理。在 `.github/release-notes/<tag>.md` 放一份手写正文可以覆盖自动生成的模板。
