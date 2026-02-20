using System.Collections.Generic;
using System.Linq;
using Colossal.Localization;
using Game.SceneFlow;

namespace CS2_Translate_Mod.Localization
{
    /// <summary>
    /// ゲームのローカライゼーションシステムに翻訳を注入するクラス。
    /// MemorySource を再利用し、蓄積を防止する。
    /// </summary>
    public static class LocalizationInjector
    {
        /// <summary>日本語ロケールID</summary>
        public const string JapaneseLocaleId = "ja-JP";

        /// <summary>ロケールIDごとに登録済みの MemorySource を保持（再利用で蓄積防止）</summary>
        private static readonly Dictionary<string, MemorySource> _registeredSources
            = new Dictionary<string, MemorySource>();

        /// <summary>このModが注入した全MemorySourceの参照リスト（抽出時のスキップ判定用）</summary>
        private static readonly List<MemorySource> _allInjectedSources = new List<MemorySource>();

        /// <summary>
        /// 翻訳辞書をゲームのローカライゼーションシステムに注入する。
        /// 同一ロケールIDに対して再注入する場合、新しい MemorySource を作成して AddSource する。
        /// 過去の MemorySource は中身を空にして実質的に無効化する。
        /// </summary>
        /// <param name="localeId">ロケールID（例: "ja-JP"）</param>
        /// <param name="translations">キー → 訳文 の辞書</param>
        /// <returns>注入に成功したかどうか</returns>
        public static bool Inject(string localeId, Dictionary<string, string> translations)
        {
            if (translations == null || translations.Count == 0)
            {
                Mod.Log.Warn("No translations to inject.");
                return false;
            }

            var localizationManager = GameManager.instance?.localizationManager;
            if (localizationManager == null)
            {
                Mod.Log.Error("LocalizationManager is not available.");
                return false;
            }

            try
            {
                // 既存のソースがあれば内容を空にして実質無効化（ゲーム側かRemoveSourceを提供しないため）
                InvalidateExistingSource(localeId);

                var source = new MemorySource(translations);
                localizationManager.AddSource(localeId, source);
                _registeredSources[localeId] = source;
                _allInjectedSources.Add(source);

                Mod.Log.Info($"Injected {translations.Count} translations for locale '{localeId}'.");
                return true;
            }
            catch (System.Exception ex)
            {
                Mod.Log.Error(ex, $"Failed to inject translations for locale '{localeId}'.");
                return false;
            }
        }

        /// <summary>
        /// 日本語ロケールに翻訳辞書を注入する（便利メソッド）。
        /// </summary>
        /// <param name="translations">キー → 訳文 の辞書</param>
        /// <returns>注入に成功したかどうか</returns>
        public static bool InjectJapanese(Dictionary<string, string> translations)
        {
            return Inject(JapaneseLocaleId, translations);
        }

        /// <summary>
        /// 既存の辞書をゲームのローカライゼーションシステムに直接注入する。
        /// 設定画面のローカライズなど、小規模な注入に使用。
        /// キーは "settings:{localeId}" で管理し、繰り返し注入でも蓄積しない。
        /// </summary>
        /// <param name="localeId">ロケールID</param>
        /// <param name="entries">キー → 訳文 の辞書</param>
        public static void InjectDictionary(string localeId, Dictionary<string, string> entries)
        {
            var localizationManager = GameManager.instance?.localizationManager;
            if (localizationManager == null)
            {
                Mod.Log.Warn("LocalizationManager not available for dictionary injection.");
                return;
            }

            try
            {
                var settingsKey = $"settings:{localeId}";
                InvalidateExistingSource(settingsKey);

                var source = new MemorySource(entries);
                localizationManager.AddSource(localeId, source);
                _registeredSources[settingsKey] = source;
                _allInjectedSources.Add(source);

                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    Mod.Log.Info($"Injected {entries.Count} dictionary entries for locale '{localeId}'.");
                }
            }
            catch (System.Exception ex)
            {
                Mod.Log.Error(ex, $"Failed to inject dictionary for locale '{localeId}'.");
            }
        }

        /// <summary>
        /// 現在のアクティブロケール名を取得する。
        /// </summary>
        /// <returns>ロケールID（取得失敗時は空文字列）</returns>
        public static string GetActiveLocaleId()
        {
            try
            {
                var localizationManager = GameManager.instance?.localizationManager;
                return localizationManager?.activeLocaleId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// アクティブソースの追跡をクリアする。
        /// 登録済みソースの辞書を空にして実質的に無効化し、追跡リストをクリアする。
        /// </summary>
        public static void ClearTrackedSources()
        {
            foreach (var kvp in _registeredSources)
            {
                InvalidateSource(kvp.Value);
            }
            _registeredSources.Clear();
            _allInjectedSources.Clear();
        }

        /// <summary>
        /// 指定のソースオブジェクトがこのModが注入したものかどうかを判定する。
        /// TranslationExtractor の Phase 1 で自身の注入ソースをスキップするために使用。
        /// </summary>
        /// <param name="source">判定対象のソースオブジェクト</param>
        /// <returns>このModが注入したソースの場合 true</returns>
        public static bool IsOurSource(object source)
        {
            if (source == null) return false;
            for (int i = 0; i < _allInjectedSources.Count; i++)
            {
                if (ReferenceEquals(source, _allInjectedSources[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 指定キーの既存ソースがあれば、内部辞書を空にして実質的に無効化する。
        /// </summary>
        private static void InvalidateExistingSource(string key)
        {
            if (_registeredSources.TryGetValue(key, out var existingSource))
            {
                InvalidateSource(existingSource);
                _registeredSources.Remove(key);
            }
        }

        /// <summary>
        /// MemorySource の内部辞書をリフレクションで空にし、ゲーム側の検索でヒットしないようにする。
        /// </summary>
        private static void InvalidateSource(MemorySource source)
        {
            if (source == null) return;
            try
            {
                // MemorySource の内部辞書をリフレクションでクリア
                var fields = source.GetType().GetFields(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (typeof(System.Collections.IDictionary).IsAssignableFrom(field.FieldType))
                    {
                        var dict = field.GetValue(source) as System.Collections.IDictionary;
                        dict?.Clear();
                    }
                }
            }
            catch (System.Exception ex)
            {
                if (Mod.ModSetting?.EnableDebugLog == true)
                    Mod.Log.Warn($"Failed to invalidate MemorySource: {ex.Message}");
            }
        }
    }
}
