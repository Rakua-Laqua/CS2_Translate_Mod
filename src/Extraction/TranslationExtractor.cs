using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Colossal;
using CS2_Translate_Mod.Models;
using Colossal.Localization;
using Game.SceneFlow;
using Newtonsoft.Json;

namespace CS2_Translate_Mod.Extraction
{
    /// <summary>
    /// ゲームのローカライゼーションシステムからMod別に翻訳キーを抽出するクラス。
    /// 
    /// 抽出方式:
    ///   Phase 1 (推奨): m_UserSources からソースごとに抽出し、ソースの型情報でMod別にグループ化。
    ///                    IDictionarySource.ReadEntries() で正しい文字列値を取得できる。
    ///   Phase 2 (フォールバック): activeDictionary の m_Dict から全エントリを取得し、キーパターンでグルーピング。
    /// </summary>
    public static class TranslationExtractor
    {
        /// <summary>
        /// エントリキーからMod識別子を推定するための正規表現パターン。
        /// 例: "Options.SECTION[Anarchy.Anarchy.AnarchyMod]" → "Anarchy"
        /// </summary>
        private static readonly Regex ModIdPattern = new Regex(
            @"\[([A-Za-z0-9_]+)[\.\[]",
            RegexOptions.Compiled);

        /// <summary>
        /// バニラ（ゲーム本体）のキープレフィックス。
        /// フォールバック時のバニラ判定に使用。
        /// </summary>
        private static readonly HashSet<string> VanillaPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assets.", "Camera.", "Chirper.", "Cinema.", "Climate.",
            "Common.", "Editor.", "Economy.", "Education.", "Electricity.",
            "Fire.", "Garbage.", "Healthcare.", "Infoviews.", "Input.",
            "Loading.", "MainMenu.", "Map.", "Media.", "Menu.",
            "Notification.", "Options.SECTION[General]", "Options.SECTION[Gameplay]",
            "Options.SECTION[Interface]", "Options.SECTION[Audio]",
            "Options.SECTION[Graphics]", "Options.SECTION[Keybinding]",
            "Panel.", "PhotoMode.", "Policies.", "Properties.",
            "SelectedInfoPanel.", "Services.", "Simulation.", "SubServices.",
            "ToolOptions.", "Tools.", "Tooltip.", "Transport.", "Tutorial.",
            "UI.", "Water.", "Zone.",
            // 追加バニラプレフィックス
            "About.", "Achievements.", "AnimationCurve.", "AudioSettings.",
            "BadInput.", "BadUserInput.", "Budget.", "Content.", "DefaultTool.",
            "DuplicateEntry.", "EditorSettings.", "EditorTutorials.",
            "EconomyPanel.", "GameListScreen.", "Gamepad.", "GameplaySettings.",
            "General.", "GeneralSettings.", "Paradox.",
        };

        /// <summary>
        /// ゲーム/エンジンのアセンブリプレフィックス（ソースベース抽出時のバニラ判定用）。
        /// これらのアセンブリから来たソースはModではなくバニラとみなす。
        /// ただし MemorySource 等の汎用型は例外的にキーで判定する。
        /// </summary>
        private static readonly string[] GameAssemblyPrefixes = new[]
        {
            "Game", "Colossal.", "Unity.", "UnityEngine",
            "System.", "mscorlib", "netstandard",
            "Newtonsoft.", "Burst.", "Unity,",
        };

        // ────────────────────────────────────────────────────────────────
        //  メイン抽出エントリポイント
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 現在のゲームのローカライゼーションからMod別にキーを抽出する。
        /// Phase 1: m_UserSources からソースベースで抽出（推奨）。
        /// Phase 2: m_Dict からフォールバック抽出。
        /// </summary>
        public static ExtractionResult ExtractAll(string outputDirectory, string sourceLocaleId = "en-US")
        {
            var result = new ExtractionResult();

            var localizationManager = GameManager.instance?.localizationManager;
            if (localizationManager == null)
            {
                Mod.Log.Error("LocalizationManager is not available for extraction.");
                result.ErrorMessage = "LocalizationManager が利用できません。";
                return result;
            }

            // デバッグ情報出力
            if (Mod.ModSetting?.EnableDebugLog == true)
            {
                LogManagerInternals(localizationManager);
            }

            // ── Phase 1: ソースベース抽出（推奨） ──
            Dictionary<string, Dictionary<string, string>> modGroups = null;
            try
            {
                modGroups = ExtractFromUserSources(localizationManager);
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"[Extraction] Source-based extraction failed: {ex.Message}");
            }

            // ── Phase 2: フォールバック（辞書ベース） ──
            if (modGroups == null || modGroups.Count == 0)
            {
                Mod.Log.Info("[Extraction] Source-based extraction yielded 0 mods. Falling back to dictionary-based extraction.");
                try
                {
                    var allEntries = GetAllLocalizationEntries(localizationManager, sourceLocaleId);
                    if (allEntries != null && allEntries.Count > 0)
                    {
                        Mod.Log.Info($"[Extraction] Dictionary-based: {allEntries.Count} entries retrieved.");
                        modGroups = GroupByMod(allEntries);
                    }
                }
                catch (Exception ex)
                {
                    Mod.Log.Error(ex, "Dictionary-based extraction also failed.");
                    result.ErrorMessage = $"ローカライゼーションエントリの取得に失敗: {ex.Message}";
                    return result;
                }
            }

            if (modGroups == null || modGroups.Count == 0)
            {
                Mod.Log.Warn("[Extraction] No mod entries found.");
                result.ErrorMessage = "Modのローカライゼーションエントリが見つかりません。";
                return result;
            }

            // 出力ディレクトリの準備
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // 各Modグループを個別のJSONファイルとして出力
            foreach (var group in modGroups)
            {
                try
                {
                    var filePath = WriteModTranslationFile(outputDirectory, group.Key, group.Value);
                    result.ExtractedFiles.Add(filePath);
                    result.TotalEntries += group.Value.Count;

                    if (Mod.ModSetting?.EnableDebugLog == true)
                    {
                        Mod.Log.Info($"  Extracted: {group.Key} ({group.Value.Count} entries)");
                    }
                }
                catch (Exception ex)
                {
                    Mod.Log.Error(ex, $"Failed to write extraction file for mod: {group.Key}");
                    result.FailedMods.Add(group.Key);
                }
            }

            result.TotalMods = modGroups.Count;
            Mod.Log.Info($"Extraction complete: {result.TotalMods} mods, {result.TotalEntries} entries, {result.ExtractedFiles.Count} files written.");

            return result;
        }

        // ────────────────────────────────────────────────────────────────
        //  Phase 1: ソースベース抽出（m_UserSources）
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// m_UserSources からソースごとにエントリを読み、Mod別にグループ化する。
        /// 
        /// m_UserSources は List&lt;ValueTuple&lt;string, IDictionarySource&gt;&gt; で、
        /// Item1=ロケールID, Item2=IDictionarySource。
        /// 
        /// ロケール優先順位:
        ///   1. en-US のソースを最優先で使用
        ///   2. en-US が無いキーについてのみ、他のロケールでフォールバック
        /// 
        /// 同じModが複数ロケールでソースを登録している場合、キーの集合和を取り、
        /// 値は en-US → その他 の優先順位で決定する。
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> ExtractFromUserSources(
            LocalizationManager localizationManager)
        {
            var modGroups = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            // m_UserSources フィールドを取得
            var field = localizationManager.GetType().GetField("m_UserSources",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Mod.Log.Warn("[Extraction] m_UserSources field not found.");
                return modGroups;
            }

            var sourcesObj = field.GetValue(localizationManager);
            if (!(sourcesObj is IEnumerable sources))
            {
                Mod.Log.Warn("[Extraction] m_UserSources is null or not IEnumerable.");
                return modGroups;
            }

            // ── ステップ1: 全ソースを解析してロケール別に収集 ──
            // キー: modName, 値: ロケール→エントリ辞書のマッピング
            var modLocaleEntries = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(
                StringComparer.OrdinalIgnoreCase);
            // 各modが持つキーの全集合（ロケール横断）
            var modAllKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            int sourceCount = 0;
            int skippedVanilla = 0;
            int skippedEmpty = 0;
            int skippedSelf = 0;

            foreach (var item in sources)
            {
                if (item == null) continue;

                var itemType = item.GetType();
                var locale = itemType.GetField("Item1")?.GetValue(item)?.ToString() ?? "";
                var sourceObj = itemType.GetField("Item2")?.GetValue(item);

                if (!(sourceObj is IDictionarySource dictSource)) continue;
                sourceCount++;

                // ソースからエントリを読み取り
                Dictionary<string, string> sourceEntries;
                try
                {
                    var errors = new List<IDictionaryEntryError>();
                    var readResult = dictSource.ReadEntries(errors, new Dictionary<string, int>());
                    sourceEntries = new Dictionary<string, string>();
                    if (readResult != null)
                    {
                        foreach (var kvp in readResult)
                        {
                            if (!string.IsNullOrEmpty(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                                sourceEntries[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch
                {
                    continue;
                }

                if (sourceEntries.Count == 0)
                {
                    skippedEmpty++;
                    continue;
                }

                // Mod名の特定
                var modName = IdentifyModFromSource(sourceObj, sourceEntries);

                if (string.IsNullOrEmpty(modName))
                {
                    skippedVanilla++;
                    continue;
                }

                if (modName.Equals("CS2_Translate_Mod", StringComparison.OrdinalIgnoreCase))
                {
                    skippedSelf++;
                    continue;
                }

                // ロケール別に蓄積
                if (!modLocaleEntries.ContainsKey(modName))
                    modLocaleEntries[modName] = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

                if (!modLocaleEntries[modName].ContainsKey(locale))
                    modLocaleEntries[modName][locale] = new Dictionary<string, string>();

                // 同一ロケール・同一Modに複数ソースがある場合はマージ
                foreach (var kvp in sourceEntries)
                {
                    modLocaleEntries[modName][locale][kvp.Key] = kvp.Value;
                }

                // キー集合も記録
                if (!modAllKeys.ContainsKey(modName))
                    modAllKeys[modName] = new HashSet<string>();
                foreach (var k in sourceEntries.Keys)
                    modAllKeys[modName].Add(k);

                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    var srcTypeName = sourceObj.GetType().FullName;
                    var asmName = sourceObj.GetType().Assembly.GetName().Name;
                    Mod.Log.Info($"[Extraction]   Source #{sourceCount}: {srcTypeName} (asm={asmName}, locale={locale}) → Mod={modName} ({sourceEntries.Count} entries)");
                }
            }

            Mod.Log.Info($"[Extraction] Phase 1 scan: {sourceCount} sources, {modLocaleEntries.Count} mods found. " +
                $"(Skipped: {skippedVanilla} vanilla, {skippedEmpty} empty, {skippedSelf} self)");

            // ── ステップ2: ロケール優先順位で結合 ──
            // 優先順位: en-US > en-* > その他（中国語含む）
            int totalEntries = 0;

            foreach (var mod in modLocaleEntries)
            {
                var modName = mod.Key;
                var locales = mod.Value;
                var merged = new Dictionary<string, string>();

                // ロケールを優先順位順にソート
                var sortedLocales = locales.Keys.OrderBy(loc => GetLocalePriority(loc)).ToList();

                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    var localeList = string.Join(", ", sortedLocales.Select(l => $"{l}({locales[l].Count})"));
                    Mod.Log.Info($"[Extraction]   Mod={modName}: locales=[{localeList}]");
                }

                // 高優先度のロケールから順にマージ（先に入った値が優先）
                foreach (var locale in sortedLocales)
                {
                    foreach (var kvp in locales[locale])
                    {
                        if (!merged.ContainsKey(kvp.Key))
                        {
                            merged[kvp.Key] = kvp.Value;
                        }
                    }
                }

                if (merged.Count > 0)
                {
                    modGroups[modName] = merged;
                    totalEntries += merged.Count;
                }
            }

            Mod.Log.Info($"[Extraction] Source-based (locale-merged): {modGroups.Count} mods, {totalEntries} entries.");

            // ── ステップ3: 類似名のModグループを統合 ──
            // 同じキーを持つが異なるMod名で分かれたグループを統合する
            // 例: "BetterBulldozer" と "Better_Bulldozer" → キー重複があれば統合
            modGroups = MergeSimilarModGroups(modGroups);

            Mod.Log.Info($"[Extraction] Source-based final: {modGroups.Count} mods (after merge).");

            return modGroups;
        }

        /// <summary>
        /// 類似名のModグループを統合する。
        /// 名前が正規化すると同一になるグループ（アンダースコア/大小文字の違い）、
        /// またはキーの重複率が高いグループを統合する。
        /// 統合先はエントリ数が最も多いグループ。
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> MergeSimilarModGroups(
            Dictionary<string, Dictionary<string, string>> modGroups)
        {
            // 正規化名 → 最大グループ名 のマッピングを作成
            var normalizedMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var modName in modGroups.Keys)
            {
                var normalized = NormalizeModName(modName);
                if (!normalizedMap.ContainsKey(normalized))
                    normalizedMap[normalized] = new List<string>();
                normalizedMap[normalized].Add(modName);
            }

            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in normalizedMap)
            {
                if (group.Value.Count <= 1)
                {
                    // 統合不要 — そのまま
                    var name = group.Value[0];
                    result[name] = modGroups[name];
                    continue;
                }

                // 複数のグループを統合
                // 最もエントリ数が多いグループを「メイン」とする
                var sorted = group.Value.OrderByDescending(n => modGroups[n].Count).ToList();
                var mainName = sorted[0];
                var merged = new Dictionary<string, string>(modGroups[mainName]);

                for (int i = 1; i < sorted.Count; i++)
                {
                    foreach (var kvp in modGroups[sorted[i]])
                    {
                        if (!merged.ContainsKey(kvp.Key))
                        {
                            merged[kvp.Key] = kvp.Value;
                        }
                    }

                    Mod.Log.Info($"[Extraction] Merged '{sorted[i]}' ({modGroups[sorted[i]].Count} entries) into '{mainName}'");
                }

                result[mainName] = merged;
            }

            // さらに: キー重複チェックで名前が異なるグループも統合
            result = MergeByKeyOverlap(result);

            return result;
        }

        /// <summary>
        /// Mod名を正規化する（統合比較用）。
        /// アンダースコア除去、小文字化して比較できるようにする。
        /// </summary>
        private static string NormalizeModName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace("_", "").Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// キーの重複率が高いグループを統合する。
        /// あるグループのキーの50%以上が別グループにも存在する場合、統合する。
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> MergeByKeyOverlap(
            Dictionary<string, Dictionary<string, string>> modGroups)
        {
            var names = modGroups.Keys.ToList();
            var mergeTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // srcName → targetName

            for (int i = 0; i < names.Count; i++)
            {
                if (mergeTarget.ContainsKey(names[i])) continue;

                for (int j = i + 1; j < names.Count; j++)
                {
                    if (mergeTarget.ContainsKey(names[j])) continue;

                    var keysA = modGroups[names[i]].Keys;
                    var keysB = new HashSet<string>(modGroups[names[j]].Keys);

                    int overlap = keysA.Count(k => keysB.Contains(k));
                    int smaller = Math.Min(keysA.Count, keysB.Count);

                    if (smaller > 0 && (double)overlap / smaller >= 0.5)
                    {
                        // 大きい方に吸収
                        if (modGroups[names[i]].Count >= modGroups[names[j]].Count)
                        {
                            mergeTarget[names[j]] = names[i];
                        }
                        else
                        {
                            mergeTarget[names[i]] = names[j];
                        }
                    }
                }
            }

            if (mergeTarget.Count == 0) return modGroups;

            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                if (mergeTarget.ContainsKey(name)) continue; // 吸収される側はスキップ

                var merged = new Dictionary<string, string>(modGroups[name]);

                // このグループに吸収されるグループを統合
                foreach (var src in mergeTarget.Where(m => m.Value.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var kvp in modGroups[src.Key])
                    {
                        if (!merged.ContainsKey(kvp.Key))
                            merged[kvp.Key] = kvp.Value;
                    }
                    Mod.Log.Info($"[Extraction] Merged '{src.Key}' into '{name}' (key overlap detected)");
                }

                result[name] = merged;
            }

            return result;
        }

        /// <summary>
        /// ロケールの優先順位を返す（小さいほど高優先）。
        /// en-US を最優先とし、英語系 → その他の順。
        /// </summary>
        private static int GetLocalePriority(string locale)
        {
            if (string.IsNullOrEmpty(locale)) return 100;

            var lower = locale.ToLowerInvariant();
            if (lower == "en-us") return 0;
            if (lower.StartsWith("en")) return 1;
            // 中国語・韓国語等は低優先（翻訳元としては英語が望ましい）
            return 50;
        }

        /// <summary>
        /// ソースの型情報とエントリキーからMod名を特定する。
        /// 
        /// 判定優先順位:
        ///   1. キーパターンから推定（最も一貫性がある。同じキーなら同じMod名になる）
        ///   2. ソース型のアセンブリ名がMod由来 → 名前空間先頭セグメント or アセンブリ名
        ///   3. どちらにも該当しない → null（スキップ）
        /// </summary>
        private static string IdentifyModFromSource(object source, Dictionary<string, string> entries)
        {
            // 1. キーパターンから推定（最優先 — 同じキーセットなら同じ名前になるため一貫性が高い）
            var keyBasedName = IdentifyModFromKeys(entries);
            if (!string.IsNullOrEmpty(keyBasedName))
                return keyBasedName;

            // 2. ソース型のアセンブリ/名前空間から推定（キーが標準カテゴリのみの場合のフォールバック）
            var sourceType = source.GetType();
            var assemblyName = sourceType.Assembly.GetName().Name;

            if (!IsGameAssembly(assemblyName))
            {
                var ns = sourceType.Namespace;
                if (!string.IsNullOrEmpty(ns))
                {
                    var firstSegment = ns.Split('.')[0];
                    var sanitized = SanitizeModId(firstSegment);
                    if (!string.IsNullOrEmpty(sanitized))
                        return sanitized;
                }
                return SanitizeModId(assemblyName);
            }

            // 3. どちらにも該当しない → バニラとしてスキップ
            return null;
        }

        /// <summary>
        /// アセンブリ名がゲーム/エンジン由来かどうかを判定する。
        /// </summary>
        private static bool IsGameAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName)) return true;

            // 完全一致チェック
            if (assemblyName.Equals("Game", StringComparison.OrdinalIgnoreCase)) return true;
            if (assemblyName.Equals("UnityEngine", StringComparison.OrdinalIgnoreCase)) return true;

            foreach (var prefix in GameAssemblyPrefixes)
            {
                if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// エントリのキーパターンからMod名を推定する。
        /// ブラケット内のnamespace第1セグメントを集計し、最頻出のものを返す。
        /// バニラキーしかない場合は null を返す。
        /// </summary>
        private static string IdentifyModFromKeys(Dictionary<string, string> entries)
        {
            var candidates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int vanillaCount = 0;

            foreach (var key in entries.Keys)
            {
                // バニラキーをカウント
                if (IsVanillaKey(key))
                {
                    vanillaCount++;
                    continue;
                }

                string modId = null;

                // パターン1: ブラケット内から → "Options.SECTION[Anarchy.Anarchy.AnarchyMod]" → "Anarchy"
                var match = ModIdPattern.Match(key);
                if (match.Success)
                {
                    modId = match.Groups[1].Value;
                }
                else
                {
                    // パターン2: ドット区切りの最初のセグメント（標準カテゴリでなければ）
                    var parts = key.Split('.');
                    if (parts.Length >= 2 && !IsStandardCategory(parts[0]))
                    {
                        modId = parts[0];
                    }
                }

                if (!string.IsNullOrEmpty(modId))
                {
                    if (!candidates.ContainsKey(modId))
                        candidates[modId] = 0;
                    candidates[modId]++;
                }
            }

            // バニラキーのみの場合 → バニラとして除外
            if (candidates.Count == 0)
                return null;

            // 最頻出の候補を返す
            var best = candidates.OrderByDescending(c => c.Value).First();
            return SanitizeModId(best.Key);
        }

        // ────────────────────────────────────────────────────────────────
        //  Phase 2 フォールバック: 辞書ベース抽出
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// LocalizationManagerから全エントリを取得する（フォールバック用）。
        /// 複数のアプローチを順次試し、最初に成功したものを返す。
        /// </summary>
        private static Dictionary<string, string> GetAllLocalizationEntries(
            LocalizationManager localizationManager, string localeId)
        {
            var entries = new Dictionary<string, string>();

            // --- アプローチ A: activeDictionary → m_Dict 経由 ---
            entries = TryGetFromActiveDictionary(localizationManager);
            if (entries.Count > 0)
            {
                Mod.Log.Info($"[Extraction] Fallback A (activeDictionary): {entries.Count} entries.");
                return entries;
            }

            // --- アプローチ B: m_Dictionaries マップ経由 ---
            entries = TryGetFromDictionariesMap(localizationManager, localeId);
            if (entries.Count > 0)
            {
                Mod.Log.Info($"[Extraction] Fallback B (m_Dictionaries): {entries.Count} entries.");
                return entries;
            }

            // --- アプローチ C: 全フィールド深掘り ---
            entries = TryGetFromDeepScan(localizationManager);
            if (entries.Count > 0)
            {
                Mod.Log.Info($"[Extraction] Fallback C (deep scan): {entries.Count} entries.");
                return entries;
            }

            Mod.Log.Warn("[Extraction] All fallback approaches failed.");
            return entries;
        }

        /// <summary>
        /// LocalizationManager の内部構造をログ出力する（デバッグ用）。
        /// </summary>
        private static void LogManagerInternals(LocalizationManager localizationManager)
        {
            try
            {
                var type = localizationManager.GetType();
                Mod.Log.Info($"[Extraction] LocalizationManager type: {type.FullName}");

                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    try
                    {
                        var val = f.GetValue(localizationManager);
                        var valInfo = val == null ? "null" : $"{val.GetType().Name}";
                        if (val is ICollection col) valInfo += $" (Count={col.Count})";
                        Mod.Log.Info($"[Extraction]   Field: {f.Name} ({f.FieldType.Name}) = {valInfo}");
                    }
                    catch { Mod.Log.Info($"[Extraction]   Field: {f.Name} ({f.FieldType.Name}) = <error>"); }
                }

                var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var p in props)
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                    try
                    {
                        var val = p.GetValue(localizationManager);
                        var valInfo = val == null ? "null" : $"{val.GetType().Name}";
                        if (val is ICollection col) valInfo += $" (Count={col.Count})";
                        Mod.Log.Info($"[Extraction]   Prop: {p.Name} ({p.PropertyType.Name}) = {valInfo}");
                    }
                    catch { Mod.Log.Info($"[Extraction]   Prop: {p.Name} ({p.PropertyType.Name}) = <error>"); }
                }
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"[Extraction] Failed to log manager internals: {ex.Message}");
            }
        }

        /// <summary>
        /// アプローチ A: activeDictionary → m_Dict 経由でエントリを取得。
        /// </summary>
        private static Dictionary<string, string> TryGetFromActiveDictionary(
            LocalizationManager localizationManager)
        {
            var entries = new Dictionary<string, string>();
            try
            {
                // activeDictionary プロパティを取得
                object activeDictObj = null;
                var prop = localizationManager.GetType().GetProperty("activeDictionary",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    activeDictObj = prop.GetValue(localizationManager);
                }

                if (activeDictObj == null)
                {
                    foreach (var fieldName in new[] { "m_ActiveDictionary", "m_activeDictionary", "activeDictionary" })
                    {
                        var fld = localizationManager.GetType().GetField(fieldName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (fld != null)
                        {
                            activeDictObj = fld.GetValue(localizationManager);
                            if (activeDictObj != null) break;
                        }
                    }
                }

                if (activeDictObj == null) return entries;

                var dictType = activeDictObj.GetType();
                Mod.Log.Info($"[Extraction] activeDictionary type: {dictType.FullName}");

                // m_Dict フィールドにアクセス（LocalizationDictionary 内部）
                var mDictField = dictType.GetField("m_Dict",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (mDictField != null)
                {
                    var mDictObj = mDictField.GetValue(activeDictObj);
                    if (mDictObj != null)
                    {
                        Mod.Log.Info($"[Extraction] m_Dict actual type: {mDictObj.GetType().FullName}");

                        // IDictionary<string, string> でキャスト試行
                        if (mDictObj is IDictionary<string, string> stringDict)
                        {
                            foreach (var kvp in stringDict) entries[kvp.Key] = kvp.Value;
                            Mod.Log.Info($"[Extraction] m_Dict as IDictionary<string,string>: {entries.Count}");
                            return entries;
                        }

                        // 非ジェネリック IDictionary フォールバック
                        // m_Dict は Dictionary<string, LocalizationDictionary.Entry> なので
                        // Entry の内部値を適切に抽出する
                        if (mDictObj is IDictionary nonGenericDict)
                        {
                            foreach (DictionaryEntry de in nonGenericDict)
                            {
                                var k = de.Key?.ToString();
                                var v = ExtractEntryValue(de.Value);
                                if (k != null && !string.IsNullOrEmpty(v))
                                    entries[k] = v;
                            }
                            Mod.Log.Info($"[Extraction] m_Dict as IDictionary (non-generic, Entry値抽出): {entries.Count}");
                            if (entries.Count > 0) return entries;
                        }
                    }
                }

                // LocalizationDictionary が IDictionarySource を実装していれば
                if (activeDictObj is IDictionarySource activeDictSource)
                {
                    Mod.Log.Info("[Extraction] activeDictionary implements IDictionarySource.");
                    var errors = new List<IDictionaryEntryError>();
                    var readEntries = activeDictSource.ReadEntries(errors, new Dictionary<string, int>());
                    if (readEntries != null)
                    {
                        foreach (var kvp in readEntries)
                        {
                            if (!string.IsNullOrEmpty(kvp.Value))
                                entries[kvp.Key] = kvp.Value;
                        }
                    }
                    if (entries.Count > 0) return entries;
                }

                // 内部フィールドを再帰探索
                entries = ExtractEntriesFromObject(activeDictObj, maxDepth: 3);
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"[Extraction] Approach A failed: {ex.Message}");
            }
            return entries;
        }

        /// <summary>
        /// アプローチ B: m_Dictionaries (ロケール→辞書マップ) から取得。
        /// </summary>
        private static Dictionary<string, string> TryGetFromDictionariesMap(
            LocalizationManager localizationManager, string localeId)
        {
            var entries = new Dictionary<string, string>();
            try
            {
                foreach (var fieldName in new[] { "m_Dictionaries", "m_dictionaries", "m_LocaleDictionaries" })
                {
                    var fld = localizationManager.GetType().GetField(fieldName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fld == null) continue;

                    var obj = fld.GetValue(localizationManager);
                    if (obj is IDictionary dictMap)
                    {
                        foreach (DictionaryEntry entry in dictMap)
                        {
                            var key = entry.Key?.ToString();
                            if (string.Equals(key, localeId, StringComparison.OrdinalIgnoreCase))
                            {
                                entries = ExtractEntriesFromObject(entry.Value, maxDepth: 3);
                                if (entries.Count > 0) return entries;
                            }
                        }
                        foreach (DictionaryEntry entry in dictMap)
                        {
                            entries = ExtractEntriesFromObject(entry.Value, maxDepth: 3);
                            if (entries.Count > 0)
                            {
                                Mod.Log.Info($"[Extraction] Used locale '{entry.Key}' as fallback.");
                                return entries;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"[Extraction] Approach B failed: {ex.Message}");
            }
            return entries;
        }

        /// <summary>
        /// アプローチ C: 全フィールドを再帰的にスキャンし、辞書を探す。
        /// </summary>
        private static Dictionary<string, string> TryGetFromDeepScan(
            LocalizationManager localizationManager)
        {
            var entries = new Dictionary<string, string>();
            try
            {
                var fields = localizationManager.GetType().GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var fld in fields)
                {
                    try
                    {
                        var obj = fld.GetValue(localizationManager);
                        if (obj == null) continue;

                        var found = ExtractEntriesFromObject(obj, maxDepth: 3);
                        if (found.Count > entries.Count)
                        {
                            entries = found;
                            Mod.Log.Info($"[Extraction] Deep scan: '{fld.Name}' yielded {found.Count} entries.");
                        }
                    }
                    catch { /* skip */ }
                }
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"[Extraction] Approach C failed: {ex.Message}");
            }
            return entries;
        }

        // ────────────────────────────────────────────────────────────────
        //  ヘルパーメソッド
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// LocalizationDictionary.Entry オブジェクトから実際の文字列値を抽出する。
        /// Entry は ToString() で型名 "Colossal.Localization.LocalizationDictionary+Entry" を返すため、
        /// 内部フィールドを直接参照して実際のテキストを取得する。
        /// </summary>
        private static string ExtractEntryValue(object entryObj)
        {
            if (entryObj == null) return "";
            if (entryObj is string s) return s;

            var type = entryObj.GetType();

            // Entry の内部フィールドから文字列値を探す（一般的な名前）
            foreach (var fieldName in new[] { "m_Value", "value", "m_Text", "text", "m_String", "m_Original" })
            {
                var fld = type.GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fld != null)
                {
                    var val = fld.GetValue(entryObj);
                    if (val is string str && !string.IsNullOrEmpty(str))
                        return str;
                }
            }

            // プロパティから探す
            foreach (var propName in new[] { "Value", "Text", "String", "Original" })
            {
                var prop = type.GetProperty(propName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanRead)
                {
                    var val = prop.GetValue(entryObj);
                    if (val is string str && !string.IsNullOrEmpty(str))
                        return str;
                }
            }

            // 全フィールドを走査してstring型を探す
            var allFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in allFields)
            {
                if (f.FieldType == typeof(string))
                {
                    var val = f.GetValue(entryObj) as string;
                    if (!string.IsNullOrEmpty(val) && !val.Contains("+Entry"))
                        return val;
                }
            }

            // 最終手段: ToString() （型名が返る場合は空文字に）
            var result = entryObj.ToString();
            if (result != null && result.Contains("+Entry"))
                return "";
            return result ?? "";
        }

        /// <summary>
        /// 任意のオブジェクトから Dictionary&lt;string, string&gt; 相当のエントリを取得する。
        /// </summary>
        private static Dictionary<string, string> ExtractEntriesFromObject(object obj, int maxDepth = 2, int currentDepth = 0)
        {
            var entries = new Dictionary<string, string>();
            if (obj == null || currentDepth > maxDepth) return entries;

            if (obj is IDictionary<string, string> dict)
            {
                foreach (var kvp in dict) entries[kvp.Key] = kvp.Value;
                return entries;
            }

            if (obj is IEnumerable<KeyValuePair<string, string>> enumKvp)
            {
                foreach (var kvp in enumKvp) entries[kvp.Key] = kvp.Value;
                if (entries.Count > 0) return entries;
            }

            if (obj is IDictionarySource dictSource)
            {
                try
                {
                    var errors = new List<IDictionaryEntryError>();
                    var sourceEntries = dictSource.ReadEntries(errors, new Dictionary<string, int>());
                    if (sourceEntries != null)
                    {
                        foreach (var kvp in sourceEntries)
                        {
                            if (!string.IsNullOrEmpty(kvp.Value))
                                entries[kvp.Key] = kvp.Value;
                        }
                    }
                    if (entries.Count > 0) return entries;
                }
                catch { }
            }

            // 内部フィールドを再帰的に掘る
            var innerFields = obj.GetType().GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in innerFields)
            {
                try
                {
                    var inner = f.GetValue(obj);
                    if (inner == null) continue;

                    var innerType = inner.GetType();
                    if (innerType.IsPrimitive || inner is string || inner is Delegate) continue;

                    var found = ExtractEntriesFromObject(inner, maxDepth, currentDepth + 1);
                    if (found.Count > entries.Count)
                    {
                        entries = found;
                    }
                }
                catch { /* skip */ }
            }

            return entries;
        }

        // ────────────────────────────────────────────────────────────────
        //  グルーピング（フォールバック用）
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// ローカライゼーションエントリをMod別にグループ化する（フォールバック用）。
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> GroupByMod(
            Dictionary<string, string> allEntries)
        {
            var groups = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var ungrouped = new Dictionary<string, string>();

            foreach (var entry in allEntries)
            {
                if (IsVanillaKey(entry.Key))
                    continue;

                var modId = ExtractModId(entry.Key);

                if (string.IsNullOrEmpty(modId))
                {
                    ungrouped[entry.Key] = entry.Value;
                    continue;
                }

                if (!groups.ContainsKey(modId))
                {
                    groups[modId] = new Dictionary<string, string>();
                }

                groups[modId][entry.Key] = entry.Value;
            }

            if (ungrouped.Count > 0)
            {
                groups["_ungrouped"] = ungrouped;
            }

            return groups;
        }

        /// <summary>
        /// バニラ（ゲーム本体）のキーかどうかを判定する。
        /// </summary>
        private static bool IsVanillaKey(string key)
        {
            foreach (var prefix in VanillaPrefixes)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// エントリキーからMod識別子を推定する（フォールバック用）。
        /// </summary>
        private static string ExtractModId(string key)
        {
            var match = ModIdPattern.Match(key);
            if (match.Success)
            {
                return SanitizeModId(match.Groups[1].Value);
            }

            var parts = key.Split('.');
            if (parts.Length >= 2)
            {
                var firstPart = parts[0];
                if (!IsStandardCategory(firstPart))
                {
                    return SanitizeModId(firstPart);
                }
            }

            return null;
        }

        /// <summary>
        /// 標準カテゴリ名かどうかを判定する（フォールバック用）。
        /// </summary>
        private static bool IsStandardCategory(string category)
        {
            var standardCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Options", "Tooltip", "Description", "SubServices", "Services",
                "Menu", "Assets", "Properties", "Notification", "Chirper",
                "Editor", "Panel", "SelectedInfoPanel", "ToolOptions", "Tools",
                "Transport", "Tutorial", "UI", "Zone", "Common", "Loading",
                "MainMenu", "Camera", "Cinema", "Climate", "Economy",
                "Education", "Electricity", "Fire", "Garbage", "Healthcare",
                "Infoviews", "Input", "Map", "Media", "PhotoMode", "Policies",
                "Simulation", "Water", "About", "Achievements", "Content",
                "General", "Paradox", "AnimationCurve", "AudioSettings",
                "BadInput", "BadUserInput", "Budget", "DefaultTool",
                "DuplicateEntry", "GameListScreen", "Gamepad",
            };

            return standardCategories.Contains(category);
        }

        /// <summary>
        /// Mod識別子をファイル名に使える安全な形式に変換する。
        /// </summary>
        private static string SanitizeModId(string modId)
        {
            if (string.IsNullOrEmpty(modId)) return null;

            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(modId.Where(c => !invalid.Contains(c)).ToArray());

            return string.IsNullOrEmpty(sanitized) ? null : sanitized;
        }

        // ────────────────────────────────────────────────────────────────
        //  ファイル出力
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 1つのModのエントリを翻訳JSONとして出力する。
        /// 既存のファイルがある場合は、既存の翻訳を保持してマージする。
        /// </summary>
        private static string WriteModTranslationFile(
            string outputDirectory, string modId, Dictionary<string, string> entries)
        {
            // Mod別サブフォルダを作成
            var modFolder = Path.Combine(outputDirectory, modId);
            if (!Directory.Exists(modFolder))
            {
                Directory.CreateDirectory(modFolder);
            }

            var fileName = $"{modId}.json";
            var filePath = Path.Combine(modFolder, fileName);

            // 旧パス（フラット構造やレガシー名）にファイルがあれば既訳をマイグレーション
            var legacyPaths = new[]
            {
                Path.Combine(outputDirectory, $"_extracted_mod_{modId}.json"),
                Path.Combine(outputDirectory, $"{modId}.json"),
            };

            // 既存ファイルがあれば読み込み、既訳を保持
            var existingTranslations = new Dictionary<string, string>();
            var pathsToCheck = new[] { filePath }.Concat(legacyPaths);
            foreach (var checkPath in pathsToCheck)
            {
                if (!File.Exists(checkPath)) continue;
                try
                {
                    var existingJson = File.ReadAllText(checkPath);
                    var existingFile = JsonConvert.DeserializeObject<TranslationFile>(existingJson);
                    if (existingFile?.Entries != null)
                    {
                        foreach (var entry in existingFile.Entries)
                        {
                            if (!string.IsNullOrEmpty(entry.Value.Translation) &&
                                !existingTranslations.ContainsKey(entry.Key))
                            {
                                existingTranslations[entry.Key] = entry.Value.Translation;
                            }
                        }
                    }

                    if (Mod.ModSetting?.EnableDebugLog == true)
                    {
                        Mod.Log.Info($"  Merging with existing: {checkPath} ({existingTranslations.Count} translations)");
                    }
                }
                catch (Exception ex)
                {
                    Mod.Log.Warn($"Failed to read existing file: {checkPath} - {ex.Message}");
                }
            }

            // 翻訳ファイルを構築
            var translationFile = new TranslationFile
            {
                ModId = modId,
                ModName = modId,
                Version = DateTime.Now.ToString("yyyy-MM-dd"),
                Entries = new Dictionary<string, TranslationEntry>()
            };

            foreach (var key in entries.Keys.OrderBy(k => k))
            {
                translationFile.Entries[key] = new TranslationEntry
                {
                    Original = entries[key],
                    Translation = existingTranslations.ContainsKey(key)
                        ? existingTranslations[key]
                        : ""
                };
            }

            var json = JsonConvert.SerializeObject(translationFile, Formatting.Indented);
            File.WriteAllText(filePath, json);

            return filePath;
        }
    }

    /// <summary>
    /// 抽出結果のサマリー。
    /// </summary>
    public class ExtractionResult
    {
        public int TotalMods { get; set; }
        public int TotalEntries { get; set; }
        public List<string> ExtractedFiles { get; set; } = new List<string>();
        public List<string> FailedMods { get; set; } = new List<string>();
        public string ErrorMessage { get; set; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage) && ExtractedFiles.Count > 0;
    }
}
