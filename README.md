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

## 简体中文

NotEnoughTranslator（NET）是 CRC3D3 的 BepInEx 5 翻译插件。

**当前状态：基础骨架阶段。** 已实现 I2 查询 Hook；通用文本和纹理替换目前仅提供资源加载与查询接口，尚未挂接具体游戏显示或资源入口。本插件不附带翻译数据。

NET 保持游戏原有语言状态，不新增语言，也不提供 JAT 式的译文语言选择。

资源沿用 JAT 去掉语言层后的目录：`BepInEx/NotEnoughTranslator` 下的 `UI/Text`、`Text`、`Texture`。可选的导出基础设施写入 `Dump` 下的对应子目录，导出文件不会被当作翻译加载。

可选的 XUAT 互操作采用 JAT 式翻译判定，不添加或检测特殊标记。具体能力边界见 [XUAT 互操作说明](Document/简体中文_SimplifiedChinese/翻译指南.md#8-xuat-互操作)。

### 文档

- [模块架构](Document/简体中文_SimplifiedChinese/模块架构.md)：模块边界、I2 行为、生命周期、接入接口与构建。
- [翻译与资源指南](Document/简体中文_SimplifiedChinese/翻译指南.md)：目录、CSV / JSONL 格式、正则规则、纹理与重载。
