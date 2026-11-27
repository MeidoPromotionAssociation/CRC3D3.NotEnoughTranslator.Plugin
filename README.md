[English](#english) | [简体中文](#简体中文)

[![GitHub Downloads](https://img.shields.io/github/downloads/MeidoPromotionAssociation/CRC3D3.NotEnoughTranslator.Plugin/total)](https://github.com/MeidoPromotionAssociation/CRC3D3.NotEnoughTranslator.Plugin/releases)

# CRC3D3.NotEnoughTranslator.Plugin

## English

NotEnoughTranslator (NET) is a BepInEx 5 translation plugin for CRC3D3.

To report a problem, set `LogLevel/日志等级` to `Debug` in the plugin configuration, restart the game, reproduce the issue, and attach `BepInEx/LogOutput.log` to your report. [GitHub Issues](https://github.com/MeidoPromotionAssociation/CRC3D3.NotEnoughTranslator.Plugin/issues)

### Highlights of NET

**Dedicated UI translation**

- Translates the game's I2 Localization terms directly and keeps the current game language in effect.
- Replaces individual uGUI sprites by name, including sprites packed into a texture atlas.

**General text translation**

- Covers KAG dialogue, speaker names and choices, as well as text displayed through TextMesh Pro and uGUI.
- Supports exact matches and regular expressions. Use the script location or the UI component to give the same source text different translations.

**Texture replacement**

- Replaces whole textures by name. Text and image features can be enabled independently.

**Background loading**

- Loads UI and general text translations in the background.

### Getting Started

#### Prerequisites

- Install **BepInEx 5** in your CRC3D3 game directory. [BepInEx](https://github.com/BepInEx/BepInEx)
- To change settings in game, you can install the BepInEx 5 version of ConfigurationManager. Its default hotkey is `F1`. [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)

#### Installation Steps

1. Download `CRC3D3.NotEnoughTranslator.Plugin-<version>.zip`. [GitHub Releases](https://github.com/MeidoPromotionAssociation/CRC3D3.NotEnoughTranslator.Plugin/releases)
2. Extract the archive into the CRC3D3 game directory and merge the included `BepInEx` folder with the existing one. The DLL goes in `BepInEx/plugins/CRC3D3.NotEnoughTranslator.Plugin.dll`.

#### Initial Setup

Launch the game once. NET creates its configuration file and resource folders under `BepInEx`.

```text
CRC3D3/  (game directory)
└─ BepInEx/
   ├─ plugins/
   │  └─ CRC3D3.NotEnoughTranslator.Plugin.dll
   ├─ config/
   │  └─ Github.MeidoPromotionAssociation.CRC3D3.NotEnoughTranslator.Plugin.cfg
   └─ NotEnoughTranslator/
      ├─ UI/
      │  ├─ Text/
      │  └─ Sprite/
      ├─ Text/
      ├─ Texture/
      └─ Dump/
         ├─ UI/
         │  ├─ Text/
         │  └─ Sprite/
         ├─ Text/
         └─ Texture/
```

### Installing Translation Files

**Obtain a CRC3D3 translation pack separately.** If it already contains `UI`, `Text`, or `Texture` folders, merge them directly into `BepInEx/NotEnoughTranslator/`. The translation files determine which language is displayed.

| Folder | Files to put there |
| --- | --- |
| `UI/Text/` | UI terms in CSV files, either loose or in ZIP archives |
| `UI/Sprite/` | PNG, JPG, or JPEG files named after individual UI sprites |
| `Text/` | General text translations in JSONL files, either loose or in ZIP archives |
| `Texture/` | PNG, JPG, or JPEG files named after whole textures |

### How NET Reads Translation Files

NET reads the files in each resource folder before scanning its subfolders. It sorts subfolders by path and files within each folder by name, using ordinal order. If multiple entries use the same key and context, the last valid entry wins. For example, put a translation pack in `01-Base` and your overrides in `99-PersonalFixes` under the same resource folder.

### Dumping Game Resources

All dump options are off by default. Enable the ones you need under `[6Dump]` in the plugin configuration.

| Setting | Output folder | Exported files |
| --- | --- | --- |
| `EnableTermDump` | `Dump/UI/Text/` | CSV with I2 text terms encountered in game that have no NET translation |
| `EnableTextDump` | `Dump/Text/` | JSONL with untranslated general text and any available context |
| `EnableSpriteDump` | `Dump/UI/Sprite/` | Individual uGUI sprites without an applied replacement, including atlas entries, as PNGs |
| `EnableTexturesDump` | `Dump/Texture/` | PNGs of whole textures encountered by the texture module that have no matching replacement |

Keep `EnableUISpriteReplace` enabled under `[3UI]` when dumping sprites. [Dump options and output files](Document/English/TranslationGuide.md#dumping-untranslated-resources)

### Frequently Asked Questions

- **How do I apply changes to translation files?** Restart the game or set and press the `ReloadTranslateResource` hotkey under `[2General]`. The hotkey is unbound by default.
- **Can I use XUnity.AutoTranslator alongside NET?** Yes. With the default settings, XUnity.AutoTranslator skips translations that NET has applied and registered, and can still process other text. [XUAT interoperability](Document/English/TranslationGuide.md#8-xuat-interoperability)

### Documentation

- Resource formats, load order, regex rules, image replacement, and dumps. [Translation and Resource Guide](Document/English/TranslationGuide.md)
- Module boundaries, hooks, lifecycle, and developer APIs. [Module Architecture](Document/English/ModuleArchitecture.md)

<br>

---

<br>

## 简体中文

NotEnoughTranslator，简称 NET，是面向 CRC3D3 的 BepInEx 5 翻译插件。

反馈问题前，请在插件配置中将 `LogLevel/日志等级` 设为 `Debug`，重启游戏并复现问题，提交时附上 `BepInEx/LogOutput.log`。

欢迎加入 Custom Maid Server! Discord 频道： [https://discord.gg/custommaid](https://discord.gg/custommaid)

### NET 的亮点

**独立的 UI 翻译**

- 直接翻译游戏通过 I2 Localization 查询的 UI 词条，保留游戏当前的语言设置。
- 支持按名称替换单张 uGUI 精灵图，包括图集中的子图。

**通用文本翻译**

- 覆盖 KAG 剧情对白、说话人名和选项，以及 TextMesh Pro 和 uGUI 显示的文字。
- 支持带上下文的精确匹配和正则表达式。同一句原文可以按剧本位置或界面组件指定不同的译文。

**整张纹理替换**

- 支持按名称替换整张纹理。文本和图片相关功能可以独立开关。

**后台加载**

- 在后台线程加载 UI 和通用文本翻译，不会影响游戏加载速度。

### 入门

#### 前置需求

- 在 CRC3D3 游戏目录安装 **BepInEx 5**。[BepInEx](https://github.com/BepInEx/BepInEx)
- 如果想在游戏内修改配置，可安装适用于 BepInEx 5 的配置管理器，默认用 `F1` 打开。[BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)

#### 安装步骤

1. 下载 `CRC3D3.NotEnoughTranslator.Plugin-<version>.zip`。[GitHub Releases](https://github.com/MeidoPromotionAssociation/CRC3D3.NotEnoughTranslator.Plugin/releases)
2. 解压到 CRC3D3 游戏目录，将压缩包里的 `BepInEx` 文件夹与现有文件夹合并。安装后的 DLL 路径为 `BepInEx/plugins/CRC3D3.NotEnoughTranslator.Plugin.dll`。

#### 首次启动

安装后启动一次游戏，NET 会在 `BepInEx` 下创建配置文件和资源目录。

```text
CRC3D3/  （游戏目录）
└─ BepInEx/
   ├─ plugins/
   │  └─ CRC3D3.NotEnoughTranslator.Plugin.dll
   ├─ config/
   │  └─ Github.MeidoPromotionAssociation.CRC3D3.NotEnoughTranslator.Plugin.cfg
   └─ NotEnoughTranslator/
      ├─ UI/
      │  ├─ Text/
      │  └─ Sprite/
      ├─ Text/
      ├─ Texture/
      └─ Dump/
         ├─ UI/
         │  ├─ Text/
         │  └─ Sprite/
         ├─ Text/
         └─ Texture/
```

### 安装翻译文件

**请另行获取 CRC3D3 翻译包。** 如果翻译包已经包含 `UI`、`Text` 或 `Texture` 文件夹，直接合并到 `BepInEx/NotEnoughTranslator/`。实际显示的译文语言由翻译文件决定。

| 目录 | 放入的文件 |
| --- | --- |
| `UI/Text/` | UI 词条 CSV，支持单独文件和 ZIP 压缩包 |
| `UI/Sprite/` | 按单张 UI 精灵名称命名的 PNG、JPG 或 JPEG |
| `Text/` | 通用文本 JSONL，支持单独文件和 ZIP 压缩包 |
| `Texture/` | 按整张纹理名称命名的 PNG、JPG 或 JPEG |

### NET 如何读取翻译文件

NET 先读取各资源目录中的直属文件，再扫描子目录。子目录按路径排序，同一目录内的文件按文件名排序，均使用 Ordinal 顺序。键和语境相同的词条重复出现时，以最后加载的有效条目为准。例如，可以在同一个资源目录下把基础翻译包放进 `01-Base`，把自己的修订放进 `99-PersonalFixes`。

### 导出游戏资源

导出选项默认关闭，需要时在插件配置的 `[6Dump]` 中启用。

| 开关 | 输出目录 | 导出的文件 |
| --- | --- | --- |
| `EnableTermDump` | `Dump/UI/Text/` | 游戏中查询过且没有 NET 译文的 I2 文本词条 CSV |
| `EnableTextDump` | `Dump/Text/` | 未翻译的通用文本 JSONL，包含可用的语境信息 |
| `EnableSpriteDump` | `Dump/UI/Sprite/` | 未替换的单张 uGUI 精灵图 PNG，包含图集子图 |
| `EnableTexturesDump` | `Dump/Texture/` | 纹理模块遇到且没有匹配替换图的原始整张纹理 PNG |

导出精灵图时，请保持 `[3UI]` 的 `EnableUISpriteReplace` 开启。[导出配置与文件说明](Document/简体中文_SimplifiedChinese/翻译指南.md#未翻译资源导出)

### 常见问题

- **修改翻译文件后怎么生效？** 重启游戏，或在 `[2General]` 中设置 `ReloadTranslateResource` 快捷键后按下。该快捷键默认未绑定。
- **可以和 XUnity.AutoTranslator 一起用吗？** 可以。默认设置下，XUnity.AutoTranslator 会跳过 NET 已应用并登记的译文，其余文本仍可交给它翻译。[XUAT 互操作说明](Document/简体中文_SimplifiedChinese/翻译指南.md#8-xuat-互操作)

### 文档

- 资源格式、加载顺序、正则规则、图片替换和资源导出。[翻译与资源指南](Document/简体中文_SimplifiedChinese/翻译指南.md)
- 模块边界、Hook、生命周期和开发接口。[模块架构](Document/简体中文_SimplifiedChinese/模块架构.md)

<br>
<br>
<br>
<br>
<br>
<br>

---

<br>
<br>
<br>
<br>
<br>
<br>


## Building and Releasing

The plugin targets .NET Framework 4.8, Unity 2022.3, and BepInEx 5. Place the required CRC3D3 assemblies in `lib/` at the repository root. [Assembly list](Document/English/ModuleArchitecture.md#9-building)

```powershell
dotnet build CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin.csproj -c Release
```

Build output / 构建产物

`CRC3D3.NotEnoughTranslator.Plugin/bin/Release/net48/CRC3D3.NotEnoughTranslator.Plugin.dll`

To publish a release, update `PluginVersion` in the main plugin class, commit the update, and push the matching tag. For example, version `0.0.2` uses tag `v0.0.2`. The Release workflow builds and publishes the ZIP. A file at `.github/release-notes/<tag>.md` takes precedence over the generated release notes.

CI uses `GAME_LIBS_TOKEN` to fetch the required assemblies. Set `GAME_LIBS_VERSION` to choose the game version, or use `GAME_LIBS_REPO` and `GAME_LIBS_REF` to override the library source. [Library download script](https://github.com/MeidoPromotionAssociation/CRC3D3.NotEnoughTranslator.Plugin/blob/main/.github/scripts/fetch_game_libs.sh)

<br>

# KISS Rule

[KISS](https://www.kisskiss.tv/) is the company/brand that makes these games.

*This Project is not owned or endorsed by KISS.

*MODs are not supported by KISS.

*KISS cannot be held responsible for any problems that may arise when using MODs.

*If any problem occurs, please do not contact KISS.

```
KISS 規約

・原作がMOD作成者にある場合、又は、原作が「カスタムメイド3D2」のみに存在する内部データの場合、又は、原作が「カスタムメイド3D2」と「カスタムオーダーメイド3D2」の両方に存在する内部データの場合。
※MODはKISSサポート対象外です。
※MODを利用するに当たり、問題が発生してもKISSは一切の責任を負いかねます。
※「カスタムメイド3D2」か「カスタムオーダーメイド3D2」か「CR EditSystem」を購入されている方のみが利用できます。
※「カスタムメイド3D2」か「カスタムオーダーメイド3D2」か「CR EditSystem」上で表示する目的以外の利用は禁止します。
※これらの事項は https://kisskiss.tv/kiss/diary.php?no=558 を優先します。

・原作が「カスタムオーダーメイド3D2(GP01含む)」の内部データのみにある場合。
※MODはKISSサポート対象外です。
※MODを利用するに当たり、問題が発生してもKISSは一切の責任を負いかねます。
※「カスタムオーダーメイド3D2」か「CR EditSystem」をを購入されている方のみが利用できます。
※「カスタムオーダーメイド3D2」か「CR EditSystem」上で表示する目的以外の利用は禁止します。
※「カスタムメイド3D2」上では利用しないで下さい。
※これらの事項は https://kisskiss.tv/kiss/diary.php?no=558 を優先します。

・原作が「CR EditSystem」の内部データのみにある場合。
※MODはKISSサポート対象外です。
※MODを利用するに当たり、問題が発生してもKISSは一切の責任を負いかねます。
※「CR EditSystem」を購入されている方のみが利用できます。
※「CR EditSystem」上で表示する目的以外の利用は禁止します。
※「カスタムメイド3D2」「カスタムオーダーメイド3D2」上では利用しないで下さい。
※これらの事項は https://kisskiss.tv/kiss/diary.php?no=558 を優先します。
```

<br>

# Disclaimer

By downloading this software, you agree to read, accept and abide by this Disclaimer, this is a developer protection
measure and we apologize for any inconvenience this may cause.

下载此软件即表示您已阅读且接受并同意遵守此免责声明，这是为了保护开发人员而采取的措施，对于由此造成的不便，我们深表歉意。

本ソフトウェアをダウンロードすることにより、利用者は本免責事項を読み、内容を理解し、全ての条項に同意し、遵守することを表明したものとみなされます。これは開発者保護のための措置であることをご理解いただき、ご不便をおかけする場合もあらかじめご了承ください。

```
English

In case of any discrepancy between the translated versions, the Simplified Chinese version shall prevail.

1. Tool Nature Statement
    This project is an open-source tool released under the BSD-3-Clause license. The developer(s) (hereinafter referred to as "the Author") are individual technical researchers only. The Author does not derive any commercial benefit from this tool and does not provide any form of online service or user account system.
    This tool is a purely local data processing tool with no content generation capabilities whatsoever. It possesses no online upload or download functionality.
    This tool is essentially a text replacer that receives a user-provided source text to target text translation table and replaces the source text with the user-provided translation in real time while the game is running. The tool itself does not generate, modify, or inject any new data content.

2. Usage restrictions
  This software shall not be used for any illegal purposes. This includes, but is not limited to, creating or disseminating obscene or illegal materials, infringing upon the intellectual property rights of others, violating platform user agreements, or any other actions that may contravene the laws and regulations of the user's jurisdiction.
    Users shall bear full responsibility for any consequences arising from violations of the law.
  
  Users must commit to:
      - Not creating, publishing, transmitting, disseminating, or storing any content that violates the laws and regulations of their jurisdiction.
      - Not creating, publishing, transmitting, disseminating, or storing obscene or illegal materials.
      - Not creating, publishing, transmitting, disseminating, or storing content that infringes upon the intellectual property rights of others.
      - Not creating, publishing, transmitting, disseminating, or storing content that violates platform user agreements.
      - Not using the tool for any activities that endanger national security or undermine social stability.
      - Not using the tool to conduct cyber attacks or crack licensed software.
      - The Author has no legal association with user-generated content.
      - Any content created using this tool that violates local laws and regulations (including but not limited to pornography, violence, or infringing content) entails legal liability borne solely by the content creator.

3. Liability exemption
  Given the nature of open-source projects:
      - The Author cannot monitor the use of all derivative code.
      - The Author is not responsible for modified versions compiled/distributed by users.
      - The Author assumes no liability for any legal consequences resulting from illegal use by users.
      - The Author provides no technical guarantee for content review or filtering.
      - The tool's operational mechanism inherently prevents it from recognizing or filtering content nature.
      - All data processing occurs solely on the user's local device; the Author cannot access or control any user data.

  Users acknowledge and agree that:
      - This tool does not generate any content; the final content is entirely dependent on its input files. The tool only performs replacement operations and cannot be held responsible for the legality, content nature, or usage scenarios of user input data.
      - This tool contains no data upload/download capabilities; all content processing is completed on the user's local device.
      - If illegal activities involving this tool are discovered, they must be reported immediately to the public security authorities.
      - The Author reserves the right to cease distribution of specific versions suspected of being abused.

4. Age and guardianship responsibility
  Users must be persons with full civil capacity (18 years of age or older). Minors are prohibited from downloading, installing or using this tool. Guardians must assume full management responsibility for device access.

5. Agreement Update
  The author has the right to update this statement through the GitHub repository. Continued use is deemed to accept the latest version of the terms.

6. Disclaimer of Warranty
  This tool is provided "AS IS" and the developer expressly disclaims any express or implied warranties, including but not limited to:
    - Warranty of merchantability
    - Warranty of fitness for a particular purpose
    - Warranty of code freedom from defects or potential risks
    - Warranty of continuous availability and technical support

7. Waiver of liability for damages
  Regardless of the use/inability to use this tool resulting in:
    - Direct/indirect property loss
    - Data loss or business interruption
    - Third-party claims or administrative penalties
  The developer shall not bear any civil, administrative or criminal liability

8. Waiver of liability for third-party reliance
  If the third-party libraries/components included or relied upon by this tool have:
    - Intellectual property disputes
    - Security vulnerabilities
    - Content that violates local laws
    - Subject to criminal or civil penalties
  The developer shall not bear joint and several liability, and users should review the relevant licenses on their own

9. Version iteration risk
  Users understand and accept:
    - Different versions of code may have compatibility issues
    - Developers are not obliged to maintain the security of old versions
    - Modifying the code on your own may lead to unforeseen legal risks

简体中文

1. 工具性质声明  
   本项目是基于 BSD-3-Clause 许可证的开源工具。开发者（以下简称"作者"）仅为个人技术研究者，不通过本工具获取任何商业利益，亦不提供任何形式的在线服务及用户账号体系。
   本工具为纯本地化数据处理工具，不具备任何内容生成能力，无任何在线上传下载功能。
   本工具本质上是一个文本替换器，其接收用户提供的原文到译文翻译表，并在游戏运行中实时替换原文为用户提供的译文，工具本身不产生、修改或注入任何新数据内容。

2. 使用限制
   本软件不得用于任何违法用途，包括但不限于制作、传播淫秽违法物品、侵害他人知识产权、违反平台用户协议的行为等可能违反所在地法律法规的违法行为。
   使用者因违反法律造成的后果需自行承担全部责任。

   用户必须承诺：  
     - 不制作、发布、传送、传播、储存任何违反所在地法律法规的内容
     - 不制作、发布、传送、传播、储存淫秽违法物品
     - 不制作、发布、传送、传播、储存侵害他人知识产权的内容
     - 不制作、发布、传送、传播、储存违反平台用户协议的内容
     - 不将工具用于任何危害国家安全或破坏社会稳定的活动
     - 不使用本工具实施网络攻击或破解正版软件
     - 开发者与用户生成内容无法律关联性
     - 任何使用本工具创建违反当地法律法规的内容（包括但不限于色情、暴力、侵权内容），其法律责任由内容创建者独立承担

3. 责任豁免  
   鉴于开源项目特性：  
     - 作者无法监控所有衍生代码的使用
     - 不负责用户自行编译/分发的修改版本
     - 不承担用户非法使用导致的任何法律责任
     - 不提供内容审核或过滤的技术保证
     - 工具运行机制决定其无法识别或过滤内容性质
     - 所有数据处理均在用户本地设备完成，开发者无法访问或控制任何用户数据

   用户知悉并同意：
     - 本工具不具备任何内容生成能力，最终内容完全取决于其输入文件。工具仅执行替换操作，无法对用户输入数据的合法性、内容性质及使用场景负责。
     - 本工具不包含任何数据上传/下载功能，所有内容生成均在用户本地设备完成
     - 如发现有人利用本工具从事违法活动，应立即向公安机关举报
     - 开发者保留停止分发涉嫌被滥用的特定版本的权利

4. 年龄及监护责任  
   用户须为完全民事行为能力人（18 周岁及以上），禁止未成年人下载、安装或使用。监护人须对设备访问承担完全管理责任。

5. 协议更新  
   作者有权通过 GitHub 仓库更新本声明，继续使用视为接受最新版本条款。

6. 担保免责  
  此工具按"原样"提供，不附带任何明示或暗示的保证，包括但不限于：
     - 适销性担保  
     - 特定用途适用性担保  
     - 代码无缺陷或潜在风险担保  
     - 持续可用性及技术支持担保  

7. 损害赔偿责任免除  
   无论使用/无法使用本工具导致：  
     - 直接/间接财产损失
     - 数据丢失或业务中断
     - 第三方索赔或行政处罚
     - 受到刑事或民事处罚
   开发者均不承担民事、行政或刑事责任  

8. 第三方依赖免责  
   本工具包含或依赖的第三方库/组件如存在：  
     - 知识产权纠纷  
     - 安全漏洞  
     - 违反当地法律的内容  
   开发者不承担连带责任，用户应自行审查相关许可  

9. 版本迭代风险  
    用户理解并接受：  
     - 不同版本代码可能存在兼容性问题  
     - 开发者无义务维护旧版本安全性  
     - 自行修改代码可能导致不可预见的法律风险


日本語

本声明の翻訳版（日本語を含む）と簡体中文原文に解釈上の相違がある場合は、簡体中文版が優先的に有効とします。

1. ツールの性質に関する声明
   本プロジェクトは、BSD-3-Clause ライセンスに基づくオープンソースツールです。開発者（以下「作者」）は個人の技術研究者に過ぎず、本ツールを通じていかなる商業的利益も得ておらず、いかなる形式のオンラインサービス及びユーザーアカウントシステムも提供しません。
   本ツールは純粋にローカル環境でのデータ処理ツールであり、いかなるコンテンツ生成能力も有しておらず、いかなるオンラインアップロード・ダウンロード機能も備えていません。
   このツールは、基本的にテキスト置換ツールであり、ユーザーが提供したソーステキストからターゲットテキストへの翻訳テーブルを受け取り、ゲームの実行中にリアルタイムでソーステキストをユーザーが提供した翻訳に置き換えます。ツール自体は、新しいデータコンテンツを生成、変更、または挿入することはありません。

2. 使用制限
   本ソフトウェアは、以下のような、所在地の法令に違反する可能性のある違法行為を含むがこれに限定されない、いかなる違法目的にも使用してはなりません：
     - わいせつ物や違法物の作成・頒布
     - 他人の知的財産権の侵害
     - プラットフォーム利用規約違反行為
   使用者は、法律違反によって生じた結果について、自ら全ての責任を負うものとします。

   ユーザーは以下を確約しなければなりません：
     - 所在地の法令に違反する内容を、作成、公開、送信、拡散、保存しないこと。
     - わいせつ物や違法物を、作成、公開、送信、拡散、保存しないこと。
     - 他人の知的財産権を侵害する内容を、作成、公開、送信、拡散、保存しないこと。
     - プラットフォーム利用規約に違反する内容を、作成、公開、送信、拡散、保存しないこと。
     - 本ツールを国家安全を脅かす、または社会の安定を破壊する活動に使用しないこと。
     - 本ツールを使用してネットワーク攻撃を実行したり、正規ソフトウェアのクラッキングを行わないこと。
     - 開発者はユーザー生成コンテンツとの法的関連性を一切有しないこと。
     - 本ツールを使用して作成された、当地の法令に違反するコンテンツ（ポルノ、暴力、著作権侵害等を含むがこれに限定されない）についての法的責任は、コンテンツ作成者が単独で負うこと。

3. 免責事項
   オープンソースプロジェクトの性質上：
     - 作者はすべての派生コードの使用状況を監視することはできません。
     - ユーザー自身がコンパイル/配布する修正版について責任を負いません。
     - ユーザーの違法使用に起因するいかなる法的責任も負いません。
     - コンテンツ審査やフィルタリングの技術的保証は提供しません。
     - ツールの動作メカニズム上、コンテンツの性質を識別またはフィルタリングすることはできません。
     - すべてのデータ処理はユーザーのローカルデバイス上で完了し、開発者はユーザーデータにアクセスまたは制御することはできません。

   ユーザーはこれを理解し同意するものとします：
     - このツールはコンテンツを生成しません。最終的なコンテンツは入力ファイルに完全に依存します。このツールは置換操作のみを実行するものであり、ユーザーが入力したデータの合法性、コンテンツの性質、または使用シナリオについては一切責任を負いません。
     - 本ツールにはいかなるデータアップロード/ダウンロード機能も含まれておらず、すべてのコンテンツ生成はユーザーのローカルデバイス上で完了します。
     - 本ツールを利用した違法行為を発見した場合は、直ちに公安機関に通報すること。
     - 開発者は、悪用の疑いのある特定バージョンの配布停止権利を留保します。

4. 年齢及び監督責任
   ユーザーは完全民事行為能力者（18歳以上）でなければなりません。未成年者のダウンロード、インストール、または使用は禁止されています。保護者はデバイスへのアクセスについて完全な管理責任を負うものとします。

5. 規約の更新
   作者は、GitHub リポジトリを通じて本声明を更新する権利を有します。継続的な使用は最新版の条項の受諾とみなされます。

6. 保証の免責
   本ツールは「現状のまま」提供され、商品性、特定目的への適合性、コードの欠陥や潜在リスクの不存在、継続的な利用可能性及び技術サポートの保証を含むがこれらに限定されない、明示または黙示を問わず、いかなる保証も付帯しません。

7. 損害賠償責任の免責
   本ツールの使用または使用不能によって生じた以下の事項について、開発者は民事、行政、または刑事上のいかなる責任も負いません：
     - 直接的または間接的な財産上の損害
     - データ損失または業務中断
     - 第三者からの請求または行政処分
     - 刑事罰または民事罰の適用

8. 第三者依存関係に関する免責
   本ツールに含まれる、または依存するサードパーティライブラリ/コンポーネントに関して：
     - 知的財産権に関する紛争
     - セキュリティ上の脆弱性
     - 当地の法律に違反する内容
   が存在する場合でも、開発者は連帯責任を負わず、ユーザーは関連ライセンスを自ら確認するものとします。

9. バージョン更新リスク
   ユーザーは以下を理解し受諾するものとします：
     - 異なるバージョンのコード間で互換性の問題が生じる可能性があること。
     - 開発者は旧バージョンのセキュリティを維持する義務を負わないこと。
     - コードの独自修正は予期せぬ法的リスクを招く可能性があること。
```

<br>
<br>
