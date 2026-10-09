# NotEnoughTranslator Module Architecture

[简体中文](../简体中文_SimplifiedChinese/模块架构.md) | [Home](../../README.md) | [Translation and resource guide](TranslationGuide.md)

This document describes the current foundation for plugin development and integration. See the [translation guide](TranslationGuide.md) for resource formats and translation-pack authoring.

## 1. Current Scope

| Module | Implemented | Not yet connected or verified |
| --- | --- | --- |
| UI / I2 | CSV loading, central query hook, parameter and AdvancedFormat processing, forced refresh | In-game UI coverage and font adaptation |
| General text | JSONL loading, exact matching, regex replacement, secondary translation of captures, query API | Specific TMP / uGUI / script display hooks |
| Texture replacement | PNG / JPG / JPEG indexing, reading data by texture name, reload and unload | Game resource-loading and UI image replacement hooks |

This is a foundation, not a claim of complete game text or image coverage. Each module can be enabled or disabled independently.

XUAT interop is shared infrastructure, not a fourth translation module. It protects recorded NET translations but does not supply the missing general text display hooks.

## 2. Design Constraints

- Do not register languages, add a `LanguageSource`, or modify the game language or `Product.supportMultiLanguage`.
- Do not provide JAT-style translation-language selection or multilingual-string splitting. A pack supplies the text to display directly.
- Do not use LRU or text hit/miss caches. Compile regex rules during loading and match them directly during queries.
- XUAT protection records returned translations only, without caching source-to-result queries or adding, detecting, or stripping special markers.
- Use .NET regex syntax with secondary exact translation of captures, without custom `Unescape` processing.
- Read JSONL only in the general text module; do not introduce a TXT compatibility layer.
- Do not port old NGUI, COM3D2 filesystem, or unverified global TMP setter patches.
- Translate at display or query boundaries, without modifying the game's business data in place.

## 3. Data Flow

```text
CSV / ZIP
  -> AsyncTranslationLoader
  -> TranslationLoadResult.I2Terms
  -> UITranslateManager
  -> UITextTranslatePatch
  -> I2 display flow

JSONL / ZIP
  -> AsyncTranslationLoader
  -> TextTranslations / TextRegexTranslations
  -> TextTranslateManager
  -> Text display hooks (not yet connected)

Images under Textures
  -> TextureReplacementCatalog path index
  -> TextureReplaceManager
  -> Resource or image display hooks (not yet connected)
```

The two text modules share loading infrastructure, but keep their resource channels and consumers separate. `TranslationCatalog` handles exact and regex queries; `ResourceModule<T>` manages loading tasks and publishes results on the main thread.

## 4. UI / I2 Query Overrides

The target is CRC's `LocalizationManager.TryGetTranslation`, including its `Localize.AdvancedFormat` parameter. If the expected signature is unavailable, the plugin logs an error and leaves the UI module disabled instead of forcing a language change.

Query behavior:

1. Preserve the original result when `overrideLanguage` is specified. Nested term queries within that call are also left unchanged.
2. Preserve the original translation and success flag when the custom dictionary has no match.
3. Do not replace resource identifiers as text when the original term metadata describes a non-text type.
4. On a match, apply localization parameters, RTL processing, and `AdvancedFormat.Format` to the new translation under the original conditions.
5. Replace the translation and set the success flag only after processing succeeds and the result is not blank.
6. On a processing exception, preserve the original result and warn only once per term per resource load.

There is no need to change the game's language list, language indices, or Source priority. See the [translation guide](TranslationGuide.md) for UI translation formatting requirements.

## 5. Lifecycle and Threading

All three managers provide `Init` / `Reload` / `Unload`, called on Unity's main thread. Resource loading is completion-driven, with no per-frame `Tick` calls.

- Translation-file reading, JSON / CSV parsing, regex compilation, and texture-path scanning run in the background without calling Unity APIs.
- Awaiting a load captures Unity's synchronization context; completion resumes on the main thread to publish the result, without polling task status.
- Reloading keeps the previous snapshot available. Cancelled or obsolete tasks cannot publish stale results.
- File-reading failures retain the previous snapshot. Successfully loading an empty directory clears that module's old resources.
- Texture bytes are read when queried, without caching image contents; path scanning and image reading are separate stages.
- Configuration-change events post reload requests to the main thread, coalescing pending requests. The plugin's `Update` only checks the reload shortcut; it does not drive resource loading or interop initialization.

### Display Refresh

After publishing UI resources, the module calls `LocalizeAll(true)` if `LocalizeManager.isSetupCompleted` is already true. Otherwise, a postfix on that property's setter triggers the pending refresh when setup completes, without a frame loop.

When UI translation is enabled, `UITranslateManager.Init` installs both the I2 translation hook and the localization-readiness hook. `Unload` removes the translation hook but retains readiness callbacks, so disabling UI translation before setup completes does not lose a pending native refresh. On plugin destruction, `UITranslateManager.Shutdown` unloads the UI module, clears pending refresh state, and removes the readiness hook. The plugin entry point only calls manager lifecycle methods; it does not manage UI patches directly. None of these operations switches the game's language.

If the game has no original translation for a term, a null I2 result merely leaves the component's current text unchanged; it does not guarantee restoration of an earlier value.

The general text and texture modules currently affect subsequent queries only. Refreshing existing display objects must be implemented alongside their specific display hooks.

### Harmony Patch IDs

Following JAT, each patch class declares an explicit, stable, lowercase `HarmonyId`. Prefixes, postfixes, and finalizers in the same patch class share that owner ID; different patch classes use separate IDs. Installation, failure rollback, and unloading use the same Harmony instance, and `UnpatchSelf()` removes only that owner's patches.

| Patch owner | Harmony ID |
| --- | --- |
| `UITextTranslatePatch` | `github.meidopromotionassociation.crc3d3.notenoughtranslator.plugin.hooks.ui.uitexttranslatepatch` |
| `LocalizationReadyPatch` | `github.meidopromotionassociation.crc3d3.notenoughtranslator.plugin.hooks.ui.localizationreadypatch` |
| `XUATInterop` | `github.meidopromotionassociation.crc3d3.notenoughtranslator.plugin.utils.xuatinterop` |

These are Harmony owner IDs, not the BepInEx plugin GUID; the plugin GUID remains unchanged. Future patch classes should declare their own IDs rather than reuse a module-level shorthand.

## 6. Integration APIs

| Manager | Consumer API | Result |
| --- | --- | --- |
| `UITranslateManager` | `TryGetTranslation(term, out translation)` | Text lookup by I2 term |
| `TextTranslateManager` | `TryGetTranslation(sourceText, out translation)` | Exact matching first, then regex rules |
| `TextureReplaceManager` | `TryGetReplacement(textureName, out data)` | Encoded replacement-image bytes |

All three expose `IsLoaded`, `IsLoading`, and `EntryCount`. The text module also exposes `ExactCount` / `RegexCount`; the UI module exposes `IsHookInstalled`.

Source entry points:

- [UITranslateManager.cs](../../CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin/Manger/UITranslateManager.cs)
- [TextTranslateManager.cs](../../CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin/Manger/TextTranslateManager.cs)
- [TextureReplaceManager.cs](../../CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin/Manger/TextureReplaceManager.cs)
- [ResourceModule.cs](../../CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin/Loader/ResourceModule.cs)

## 7. XUAT Interoperability

Following JAT's decision-hook approach, NET locates the loaded `XUnity.AutoTranslator.Plugin.Core` assembly through reflection. No compile-time XUAT reference or DLL in `lib` is required. Compatible targets are `IsTranslatable` methods returning `bool` with a first parameter of type `string` on these types:

- `Utilities.LanguageHelper`
- `TextTranslationCache`
- `CompositeTextTranslationCache`

Decision order:

1. If the input is a recorded NET UI or general text translation, return `false` to prevent XUAT from translating it again.
2. If general text display hooks have taken control and the input has an exact or regex translation, also return `false` and leave it to NET.
3. Otherwise preserve XUAT's original decision. Exceptions in NET's decision logic also preserve original behavior rather than blocking every string.

**Step 2 is currently disabled by default.** General text has no actual display hooks yet; loading a dictionary does not guarantee that NET will assign text to a component. Future integration code must call `TextTranslateManager.SetDisplayHooksInstalled(true)` after installing its display hooks successfully, and pass `false` when removing them. Module unloading also resets this state. It is not a translation-pack setting.

Translation recording and query APIs:

- With interop active, general text `TryGetTranslation` skips recorded NET translations and records successful results before returning them.
- `IsInTranslationDictionary` only probes translation availability without recording candidate results; a probe is not treated as output.
- UI calls `MarkTranslated` only after parameter, RTL, and `AdvancedFormat` processing succeeds, recording final results rather than unexpanded templates.
- Both managers expose `IsNetTranslatedText` and `MarkTranslated`. Future display hooks that further modify a returned value should record the final text they actually submit.
- UI and general text use separate, thread-safe sets with Ordinal matching. Publishing new resources successfully or unloading a module clears its own set; failed loads preserve the previous state.
- These sets only prevent retranslation; they never return cached translations and contain no miss records or LRU logic. No additional normalization, unescaping, or placeholder inference is performed.

Initialization runs from plugin `Start`. Late assembly-load events post a callback to Unity's synchronization context; actual patching runs on the main thread without `Update` polling. Queued callbacks do nothing after interop is unloaded. Without XUAT, NET behaves normally. Incompatible signatures or installation failures produce warnings. Unloading removes only NET's own Harmony patches and unsubscribes from assembly-load events.

`EnableXUATInterop/启用 XUAT 互操作` in `2General` is enabled by default. `XUATInterop.IsInstalled` and `PatchedMethodCount` expose integration status. Reflection compatibility checks do not replace validation with a real XUAT version or the game.

See [XUATInterop.cs](../../CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin/Utils/XUATInterop.cs) and [TranslatedTextRegistry.cs](../../CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin/Translation/TranslatedTextRegistry.cs).

## 8. Future Integration Requirements

- General text hooks must replace display values only, without modifying names that double as business keys, CSV data, or game state.
- Isolate I2 call context to avoid translating an I2-produced display value a second time.
- Do not permanently exclude a component merely because it has `Localize`; the game can still assign text to that same component directly.
- Texture integration must create and manage `Texture` / `Sprite` objects on Unity's main thread, with clear ownership of original and replacement resources.
- Do not treat the current query APIs or loaded-entry counts as evidence of in-game coverage.

## 9. Building

Target environment: .NET Framework 4.8 / Unity 2022.3 / BepInEx 5.

Place matching CRC versions of these assemblies in the repository-root `lib` directory:

- `I2.Localization.dll`
- `Assembly-CSharp.dll`
- `Assembly-CSharp-firstpass.dll`

Run from the repository root:

```powershell
dotnet build CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin/CRC3D3.NotEnoughTranslator.Plugin.csproj -c Release
```

Async code uses `Task`, without adding a `System.Threading.Tasks.Extensions` / `ValueTask` runtime dependency. A successful build does not replace in-game verification.
