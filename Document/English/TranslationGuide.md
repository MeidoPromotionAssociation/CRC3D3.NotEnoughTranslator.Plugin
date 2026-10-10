# NotEnoughTranslator Translation and Resource Guide

[简体中文](../简体中文_SimplifiedChinese/翻译指南.md) | [Home](../../README.md) | [Module architecture](ModuleArchitecture.md)

This document covers NET's resource directories, formats, matching rules, and reloading. See [module architecture](ModuleArchitecture.md) for implementation status and development APIs.

**Current status: foundation stage.** UI has an I2 query hook; general text and textures are not yet connected to specific game display hooks. Successfully loading resources does not mean translations or replacement images are already displayed in the game.

## 1. Resource Directories

- Configuration file: `BepInEx/config/Github.MeidoPromotionAssociation.CRC3D3.NotEnoughTranslator.Plugin.cfg`
- Translation root: `BepInEx/NotEnoughTranslator`
- UI text directory: `BepInEx/NotEnoughTranslator/UI/Text`
- General text directory: `BepInEx/NotEnoughTranslator/Text`
- Texture directory: `BepInEx/NotEnoughTranslator/Texture`
- Dump root: `BepInEx/NotEnoughTranslator/Dump`

All paths are relative to the game root.

```text
BepInEx/
  NotEnoughTranslator/
    UI/
      Text/
        interface.csv
        ui-pack.zip
    Text/
      common.jsonl
      text-pack.zip
    Texture/
      button.png
    Dump/
      UI/
        Text/
      Text/
      Texture/
```

NET follows JAT's module directory layout without the target-language layer. The UI module recursively reads `.csv` files only under `UI/Text`; general text reads `.jsonl` only under `Text`; textures are indexed only under `Texture`. These are actual loading boundaries, not merely suggested organization. Files or ZIP packs placed directly in the translation root are not loaded, and `Dump` is never scanned for translations.

When migrating the old layout, move UI CSV files into `UI/Text`, general-text JSONL files into `Text`, and rename `Textures` to `Texture`. Place ZIP packs inside the corresponding text module's directory; there is no compatibility scan of the old shared root.

General text does not read `.txt`. There are no target-language subdirectories or language-selection options: a pack directly supplies the translations to display.

Both text modules can read matching entries directly from ZIP archives in their own directories without extracting them to disk. Nested ZIP archives are not expanded. The texture module currently indexes loose PNG / JPG / JPEG images under `Texture` only, not images inside ZIP archives.

## 2. Loading Order and Overrides

1. Within each module, read files directly in its resource directory first, sorted by filename using Ordinal comparison.
2. Read that module's subdirectories in Ordinal directory-name order, also sorting files within each directory by Ordinal comparison.
3. Process a ZIP at its own position in that sequence. Entries are sorted by their full names by default; disabling `AllowFilesInZipLoadInOrder` uses their original archive order instead.
4. For duplicate UI terms or exact source strings, a later valid entry overrides an earlier one.
5. Try regex rules in reverse loading order, giving later rules priority. Stop after the first successful rule; do not chain further rules.

Exact text matching always takes precedence over regex rules. Loading order is not game display order or a translation hit-rate measurement.

## 3. UI Translation: CSV

Use UTF-8 CSV. These examples use Japanese source text and Chinese translations:

```csv
Term,Original,Translation
Example/UI/Confirm,はい,确定
Example/UI/Cancel,いいえ,取消
```

| Column | Purpose |
| --- | --- |
| `Term` | The game's I2 term, used as an exact lookup key |
| `Original` | Reference text for translators; not used for matching |
| `Translation` | The translation to display |

- Use real game terms. The `Example/...` keys above are placeholders, not confirmed game terms.
- With a header, `Term` (or `Key`) and `Translation` are located by column name. Reordered columns and case-insensitive headers are supported.
- Headerless three-column files are also supported: term, reference original, translation.
- Leading and trailing whitespace is trimmed from terms. Empty terms or blank translations are ignored.
- Quoted fields, commas within fields, doubled quotes (`""`), and actual line breaks inside quotes are supported. Comment lines can start with `#` or `//`.
- Backslashes in CSV do not receive custom unescaping. Use actual line breaks within a quoted field when a translation needs multiple lines.
- The UI hook does not translate known non-text terms, such as font or sprite resource identifiers, as display text.

### Parameters and Braces

When a term actually uses `AdvancedFormat`, preserve required placeholders such as `{0}`. Literal braces follow `string.Format` rules: `{{` and `}}`.

If I2 translation processing fails, NET retains the game's original result and warns only once per term per resource load. Do not change the game language or force global multilingual support to work around missing translations.

## 4. General Text: JSONL

Each UTF-8 line contains one JSON object:

```jsonl
{"original":"はい","text":"是"}
{"original":"アリス","text":"爱丽丝"}
{"original":"^(?<name>.+)の所持金：(?<amount>\\d+)円$","text":"${name} 的持有金：${amount} 日元","regex":true}
{"original":"^残り (\\d+) 秒$","text":"剩余 $1 秒","regex":true}
```

| Field | Type | Purpose |
| --- | --- | --- |
| `original` | Required, non-empty string | Exact source text or regex pattern |
| `text` | Required string | Translation or regex replacement string |
| `regex` | Optional boolean | `true` selects regex; omitted or `false` selects exact matching |

General text JSONL requires `original`; `term` is not an alias. The UI CSV `Term` column is unchanged.

NET does not use JAT's TXT files, a leading `$` regex marker, or custom multilingual-string syntax.

### 4.1 Exact Matching

Source strings use Ordinal, case-sensitive exact matching. They are not trimmed or implicitly normalized. Blank translations are ignored.

### 4.2 Regex Replacement

A regex rule uses two distinct kinds of syntax:

| Field | Syntax | Example |
| --- | --- | --- |
| `original` | A .NET regex **pattern**, determining what matches | `^残り (\d+) 秒$` |
| `text` | A .NET regex **replacement string**, determining how to assemble the translation | `剩余 $1 秒` |

The table shows values after JSON decoding; backslashes must be written as `\\` in the actual JSONL. A complete entry is:

```jsonl
{"original":"^残り (\\d+) 秒$","text":"剩余 $1 秒","regex":true}
```

Input `残り 30 秒` produces `剩余 30 秒`:

1. `^` matches the start of the string. `残り ` and ` 秒` match literally, including their spaces.
2. `(\d+)` captures the consecutive digits `30` as capture group 1.
3. `$` matches the end; `$1` in `text` inserts the value of group 1.

`残り30秒` does not match this rule because it lacks the spaces required by the pattern.

Queries follow this order:

With XUAT interop active, general text queries first skip recorded NET translations to prevent retranslation. Other inputs follow the order below; see section 8 for the integration boundaries.

1. Look up the **entire input in the exact dictionary** first. Return immediately on a hit without trying regex rules.
2. Otherwise, try regex rules in reverse [loading order](#2-loading-order-and-overrides), with later-loaded rules first.
3. The first matching rule uses `Regex.Replace` to replace **all its matches**, preserving unmatched text.
4. Return that rule's result. Do not chain subsequent rules or translate the generated text again. A rule still counts as a match if its result is unchanged or empty.

Set `"regex":true`; without it, even a regex-looking `original` is just an exact source string. Do not use JavaScript's `/pattern/g` notation or JAT's leading `$` marker. There are no LRU, hit, or miss caches; regex rules are compiled during loading.

### 4.3 Common Pattern Syntax

The following examples show **regex patterns after JSON decoding**. Escape backslashes as explained in section 4.6 when writing them in JSONL:

| Syntax | Meaning and caveats |
| --- | --- |
| `^`, `$` | Normally match the start and end of the string; `$` also matches before a final newline. With `(?m)`, they also match line boundaries |
| `\A`, `\z` | Match the absolute start and end of the entire string, unaffected by `(?m)`; recommended for strict whole-input matching |
| `(expression)`, `$1` | Parentheses in the pattern create a numbered capture group; `$1` references it in the replacement |
| `(?<name>expression)`, `${name}` | Create and reference a named capture group; names help prevent reference mistakes when parentheses are rearranged |
| `(?:expression)` | Group an expression without capturing it |
| `[0-9]+` | One or more ASCII digits; `\d+` also matches other Unicode decimal digits, including full-width digits |
| `.`, `.+`, `.*` | Any one, one or more, or zero or more characters; by default, `.` does not match newline `\n` |
| `.*?`, `.+?` | Lazy matching, preferring shorter matches; not automatically faster or safer |
| `[^：\r\n]+` | One or more characters other than a full-width colon or line break, useful for bounding fields such as names |
| `\s*`, `\s+` | Zero or more, or one or more whitespace characters, **including line breaks**; use `[ \t]*` / `[ \t]+` for ordinary spaces and tabs only |
| `\r?\n` | Match a common LF or CRLF line break |
| `?`, `{2,4}` | Match the preceding element zero or one time, or between 2 and 4 times |
| `\.`, `\[`, `\(` | Match a literal period, opening square bracket, or opening parenthesis instead of treating it as regex syntax |
| `(?i)` | Make the following pattern case-insensitive; matching is case-sensitive by default |
| `(?m)` | Multiline mode changes `^` / `$`; it does **not** make `.` span lines |
| `(?s)` | Singleline mode makes `.` match newlines too; despite its name, it is useful for capturing multiline content |

`A|B` selects either alternative. For whole-string matching, use `^(?:A|B)$`, not `^A|B$`. Inline options can be combined: `(?is)` enables both case-insensitive matching and a dot that spans lines.

### 4.4 Secondary Translation of Captures

When a capture group is referenced, NET looks up **its entire captured value** once in the general text exact dictionary before inserting it. This retained translation feature is not an additional escaping syntax.

```jsonl
{"original":"アリス","text":"爱丽丝"}
{"original":"^(?<name>.+)の所持金：(?<amount>[0-9]+)円$","text":"${name}的持有金：${amount}日元","regex":true}
```

| Input | Output |
| --- | --- |
| `アリスの所持金：100円` | `爱丽丝的持有金：100日元` |
| `ボブの所持金：50円` | `ボブ的持有金：50日元` |

For the first input:

1. `name` captures `アリス`, which has the exact translation `爱丽丝`.
2. `amount` captures `100`, which has no exact translation and is preserved.
3. The replacement assembles the final text using these two values.

Important boundaries:

- Secondary translation uses the currently loaded **general text exact dictionary**, not UI CSV / I2 terms.
- All referenced captures, including numbers, use the same lookup; this is not limited to particular group names. Whitespace inside a captured value participates in exact matching.
- Captures are not split into words, processed by regex rules again, or recursively translated after a dictionary hit.
- An entry `アリス → 爱丽丝` does not automatically turn the captured value `アリスさん` into `爱丽丝さん`. Capture the suffix separately or add an exact entry for the complete value.
- `$1`, `${name}`, and similar text inside a captured value or its translation are inserted literally, not interpreted as another round of replacement references.

### 4.5 Replacement Syntax Reference

The following syntax applies only to `text` in regex rules and is interpreted by .NET's `Match.Result`:

| Syntax | Meaning | Secondary exact translation |
| --- | --- | --- |
| `$1`, `${1}` | Capture group 1 | Yes, when the capture succeeded |
| `${name}` | The capture group named `name` | Yes, when the capture succeeded |
| `$0`, `${0}`, `$&` | The current complete match, not necessarily the entire input | Yes |
| `$+` | .NET's last-captured-group reference | Yes |
| `$$` | One literal dollar sign `$` | No |
| `` $` `` | The part of the original input before the current match | No |
| `$'` | The part of the original input after the current match | No |
| `$_` | The entire original input | No |

- Use `${1}0` for “group 1 followed by the character `0`”; `$10` references group 10, not `$1` followed by `0`.
- `\1` is not a capture reference in a replacement string. I2 / `string.Format` syntax such as `{0}` is not interpreted by NET's general text module either.
- A valid optional group that did not participate expands to an empty string. Misspelled or nonexistent group references may remain literal under .NET rules; do not assume they raise an error.
- Referencing a repeatedly captured group uses its final `Group.Value`; it does not concatenate the group's capture history.
- `Regex.Replace` already preserves unmatched context. Prefix, suffix, or whole-input references may duplicate that context, so prefer named captures for ordinary translation rules.

For example, to print a literal `$` before a captured number:

```jsonl
{"original":"^価格：([0-9]+)$","text":"价格：$$$1","regex":true}
```

Input `価格：100` produces `价格：$100`: `$$` prints the dollar sign, then `$1` prints the number. Exact entries do not interpret replacement references in `text`; write `$` directly there.

### 4.6 JSON Escaping Versus Regex Syntax

**No custom unescaping does not mean backslashes can be written arbitrarily in JSON.** Data passes through two standard interpretation steps:

```text
JSONL file -> JSON string decoding -> original as a regex pattern / text as a replacement string
```

| Goal | String in the JSON file | Value or effect after JSON decoding |
| --- | --- | --- |
| Match digits in `original` | `"\\d+"` | Regex `\d+` |
| Match a literal period in `original` | `"\\."` | Regex `\.` |
| Match one backslash in `original` | `"\\\\"` | Regex `\\` |
| Output a real newline in `text` | `"第一行\n第二行"` | Output two lines |
| Output literal `\n` in `text` | `"第一行\\n第二行"` | Output `第一行\n第二行`, without a line break |
| Output quotation marks in `text` | `"她说：\"你好\""` | Output `她说："你好"` |
| Output a path in `text` | `"C:\\Mods"` | Output `C:\Mods` |
| Output `$` in regex `text` | `"$$"` | Output `$`; this is replacement syntax, not JSON escaping |

Writing `"\d+"` directly in JSON is invalid; write `"\\d+"` instead. Replacement references such as `$1` / `${name}` need no additional JSON escaping.

Although `"\\n"` decodes to a backslash followed by n in both fields, the regex engine interprets it as matching a newline in `original`, whereas `text` preserves those two characters. To insert a newline into a translation, use JSON's `"\n"`.

Each JSONL object must still occupy one physical line. Use JSON escapes for embedded line breaks instead of manually splitting an object across lines. Do not add comments or trailing commas.

NET does not apply custom `Unescape` or `Regex.Unescape` processing to translation contents.

### 4.7 More Complete Examples

The following example groups are independent and can each be saved as JSONL. Inputs and outputs describe translation queries, not proof that pending game display hooks are already connected.

#### Reordering Values and Allowing Spaces

```jsonl
{"original":"\\A所持数：[ \\t]*(?<current>[0-9]+)[ \\t]*/[ \\t]*(?<limit>[0-9]+)\\z","text":"上限 ${limit}，当前 ${current}","regex":true}
```

Input `所持数： 3 / 10` produces `上限 10，当前 3`. Named references can reorder parameters. `[ \t]*` allows spaces and tabs without consuming line breaks.

#### Partial Replacement and Multiple Matches

```jsonl
{"original":"HP","text":"体力"}
{"original":"MP","text":"魔力"}
{"original":"(?<stat>HP|MP):[ \\t]*(?<value>[0-9]+)","text":"${stat}：${value}","regex":true}
```

Input `HP: 10 / MP: 5` produces `体力：10 / 魔力：5`. Without whole-input anchors, the same rule replaces two matches while preserving the slash and its surrounding spaces. Secondary capture translation maps `HP` / `MP` to their Chinese equivalents.

#### Matching Literal Square Brackets

```jsonl
{"original":"設定","text":"设置"}
{"original":"\\A\\[体験版\\] (?<title>.+)\\z","text":"[体验版] ${title}","regex":true}
```

Input `[体験版] 設定` produces `[体验版] 设置`. Square brackets in `original` need regex escaping; brackets in `text` are ordinary text and need no escaping.

#### Multiline Captures and Output Line Breaks

```jsonl
{"original":"1行目\n2行目","text":"第一行\n第二行"}
{"original":"(?s)\\A説明：(?<body>.+)\\z","text":"说明：\n${body}","regex":true}
```

Two-line input:

```text
説明：1行目
2行目
```

Three-line output:

```text
说明：
第一行
第二行
```

`(?s)` lets `body` span lines, and the entire captured `1行目\n2行目` matches the exact entry. Separate entries for `1行目` and `2行目` would not provide the same secondary translation because captures are not automatically split into lines.

#### Deleting a Prefix with an Empty Replacement

```jsonl
{"original":"^\\[仮\\][ \\t]*","text":"","regex":true}
```

Input `[仮] 設定` produces `設定`. This only removes the prefix. Even if an exact entry `設定 → 设置` exists, the generated result is not sent through the dictionary again.

### 4.8 Exact Entries and Regex Rule Priority

Write these entries in this order in one file:

```jsonl
{"original":"^残り (?<seconds>[0-9]+) 秒$","text":"剩余 ${seconds} 秒","regex":true}
{"original":"^残り 0 秒$","text":"时间到","regex":true}
{"original":"残り 5 秒","text":"最后五秒"}
```

| Input | Output | Reason |
| --- | --- | --- |
| `残り 0 秒` | `时间到` | The later-loaded specific regex is tried first |
| `残り 5 秒` | `最后五秒` | A whole-input exact entry always takes priority over regex |
| `残り 10 秒` | `剩余 10 秒` | The specific regex does not match, so the general regex is tried |

Putting a broad rule later can hide earlier specific rules. `.*` can even match empty strings and insert content multiple times; it is unsuitable as a generic missing-translation fallback. Prefer patterns with explicit prefixes, separators, and field boundaries.

### 4.9 Errors, Timeouts, and Troubleshooting

Malformed JSONL entries or regex patterns are logged with their file and line number, then skipped without stopping other entries. A regex execution timeout disables that rule until the next reload to avoid blocking every frame repeatedly. The default timeout is 100ms, configurable through `RegexTimeoutMilliseconds`.

If a rule does not work or produces unexpected output, check in this order:

1. Is `"regex":true` set, are JSON backslashes escaped correctly, and does the log say the entry was skipped?
2. Is there a whole-input exact entry or a later-loaded regex that matches first?
3. Do half-width / full-width punctuation, spaces, casing, line breaks, and rich-text tags match the pattern? The engine does not strip tags or normalize whitespace first.
4. Do capture references use the correct group names, and do secondary translation entries match entire captured values exactly?
5. Has the rule been disabled after a timeout? Reload after correcting it. Avoid nested repetition such as `(.+)+` that can cause excessive backtracking instead of just increasing the timeout.

Choose a **.NET** engine in external regex tools. Validate the JSON-decoded pattern first, then check the actual JSONL. Other engines may have incompatible syntax, replacement references, or options.

## 5. Texture Replacement Resources

Place PNG / JPG / JPEG files in `Texture` or its subdirectories.

- Matching is case-insensitive by filename, ignoring directories and `.tex` / `.png` / `.jpg` / `.jpeg` extensions.
- For example, `button`, `button.tex`, and `button.png` can all query a replacement file named `button.png`.
- A later file with the same name overrides an earlier one and produces a warning. Subdirectories do not create separate texture namespaces.
- Only a path index is stored. Files are read when queried; image contents are not cached, and game textures are neither modified nor destroyed.

`TextureReplaceManager.TryGetReplacement(textureName, out byte[] data)` returns encoded image data, or `false` when no replacement is found. This API does not mean game replacement is already connected: resource hooks and Unity object management are still pending. See [module architecture](ModuleArchitecture.md).

## 6. Module Switches and Reloading

- Each module has an `Enabled/启用` switch in its respective `3UI`, `4Text`, or `5Textures` configuration section.
- Changing a switch, regex timeout, or ZIP loading order triggers resource reloading on the main thread.
- Configure the `ReloadTranslateResource` shortcut to reload manually. It is unbound by default and triggers only once per key press.
- File changes are not watched automatically. Trigger a reload after editing translation files.
- The previous resource snapshot remains available during loading. File-reading failures retain it; successfully reading an empty directory clears old resources.
- Individual malformed JSONL entries are skipped as described earlier, rather than treated as a failure to read the entire batch.

UI reloading forces an I2 refresh. General text and textures currently affect subsequent queries only. If the game has no original translation for a term, a null I2 result does not guarantee restoration of an earlier component text value.

### Dumping Untranslated Resources

The plugin creates `Dump/UI/Text`, `Dump/Text`, and `Dump/Texture` at startup. Dumping is opt-in through the `6Dump` section; all three dump switches are disabled by default.

- `EnableTermDump`: after the first UI snapshot is published, untranslated text-term queries produce CSV records with `Term,Original,Translation`. `Original` is the game's returned display text and `Translation` starts empty. Non-text terms and explicit-language queries are excluded. The UI module must remain enabled; triggering a resource reload can capture another localization pass for currently displayed UI.
- `EnableTextDump`: misses from the loaded general-text query API produce JSONL records such as `{"original":"source text","text":""}`. Dictionary-only probes do not dump. Actual game-text capture still depends on the pending display hooks.
- `EnableTexturesDump`: allows `DumpManager.DumpTexture(textureName, pngData)` to write captured original PNG data. The interface does not enumerate game textures, read GPU data, or implement capture hooks. Existing files are not overwritten, and incomplete temporary writes are not published as PNG files.
- UI terms and general text are deduplicated separately with Ordinal matching for the plugin session. Hot reloads do not reset these records. CSV quoting and JSON escaping preserve commas, quotes, backslashes, and line breaks; there is no normalized TXT copy.
- `TermDumpThreshold` and `TextDumpThreshold` each default to `100`. Reaching a threshold, pressing the optional `FlushDump` shortcut, changing dump settings, reloading resources, or shutting down the plugin flushes buffered records. There is no periodic or per-frame flush. Failed writes retain their buffers for a later explicit retry; a process crash can still lose unwritten records.

CSV and JSONL dumps use separate timestamped session files. Fill in their empty translation fields, then place the completed files under `UI/Text` or `Text` and reload. PNG dumps belong under `Texture` after editing. Leaving files under `Dump` never activates them as translation resources.

## 7. Examples and Limitations

The examples in sections 3 and 4 illustrate the formats; they are not a game translation pack. Loaded-entry counts do not measure in-game coverage. Do not modify original data that doubles as a business key. See [module architecture](ModuleArchitecture.md) for display integration, I2 isolation, and texture-lifetime requirements.

## 8. XUAT Interoperability

NET can coexist with XUnity.AutoTranslator through JAT-style `IsTranslatable` decision hooks, without adding or detecting special markers in text. Translation packs keep their normal CSV / JSONL formats and need no invisible characters or additional escaping.

- `EnableXUATInterop/启用 XUAT 互操作` in `2General` is enabled by default. Without XUAT, integration is skipped; XUAT is not a required dependency.
- UI / general text translations actually returned and recorded by NET are excluded from XUAT's translation decisions. Dynamic regex results can be recorded too, not just static dictionary values.
- Exact and regex queries determine whether NET can translate an original string, but XUAT is prevented from taking it only after general text display hooks have actually taken control.
- **General text is still a query foundation without actual display hooks, so original strings are currently left to XUAT by default.** Interop does not make JSONL translations appear by itself or interfere with XUAT's normal handling of text not taken over by NET.
- XUAT translation callbacks are not used as a replacement for display hooks. NET does not change XUAT's language, translation files, or marker settings.
- Successful resource reloads or module unloading clear that module's translation records. Existing components still follow the refresh limitations described earlier; recording text does not itself refresh the display.

Translation records are deduplication sets, not source-to-result caches, and introduce no LRU. Decision failures leave XUAT in control instead of blocking all text. See [module architecture](ModuleArchitecture.md#7-xuat-interoperability) for integration APIs, display-ownership state, and lifecycle.
