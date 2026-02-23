using System.Collections.Generic;
using Colossal.Localization;
using Game.SceneFlow;

namespace CS2_Translate_Mod.Localization
{
    /// <summary>
    /// ゲームのローカライゼーションシステムに翻訳を注入するユーティリティ。
    /// </summary>
    public static class LocalizationInjector
    {
        /// <summary>日本語ロケールID</summary>
        public const string JapaneseLocaleId = "ja-JP";

        /// <summary>ロケールID(または内部キー)ごとに登録済みの MemorySource</summary>
        private static readonly Dictionary<string, MemorySource> _registeredSources
            = new Dictionary<string, MemorySource>();

        /// <summary>本Modが注入した MemorySource の参照追跡</summary>
        private static readonly List<MemorySource> _allInjectedSources = new List<MemorySource>();

        /// <summary>診断用: 追跡している注入ソース総数</summary>
        public static int GetTrackedSourceCount() => _allInjectedSources.Count;

        /// <summary>診断用: 現在有効な登録キー数</summary>
        public static int GetRegisteredSourceCount() => _registeredSources.Count;

        /// <summary>
        /// 翻訳辞書を指定ロケールに注入する。
        /// </summary>
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
                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    Mod.Log.Info($"[Injector] Inject start locale={localeId}, entries={translations.Count}, trackedSources={_allInjectedSources.Count}, registeredSources={_registeredSources.Count}.");
                }

                // RemoveSource API がないため、既存参照は内容クリアで無効化する。
                InvalidateExistingSource(localeId);

                var source = new MemorySource(translations);
                localizationManager.AddSource(localeId, source);
                _registeredSources[localeId] = source;
                _allInjectedSources.Add(source);

                Mod.Log.Info($"Injected {translations.Count} translations for locale '{localeId}'.");

                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    Mod.Log.Info($"[Injector] Inject end locale={localeId}, trackedSources={_allInjectedSources.Count}, registeredSources={_registeredSources.Count}.");
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Mod.Log.Error(ex, $"Failed to inject translations for locale '{localeId}'.");
                return false;
            }
        }

        /// <summary>
        /// 日本語ロケールに翻訳辞書を注入する。
        /// </summary>
        public static bool InjectJapanese(Dictionary<string, string> translations)
        {
            return Inject(JapaneseLocaleId, translations);
        }

        /// <summary>
        /// 小規模辞書（設定UI等）を注入する。
        /// </summary>
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

                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    Mod.Log.Info($"[Injector] Dictionary inject start key={settingsKey}, entries={entries?.Count ?? 0}, trackedSources={_allInjectedSources.Count}, registeredSources={_registeredSources.Count}.");
                }

                InvalidateExistingSource(settingsKey);

                var source = new MemorySource(entries);
                localizationManager.AddSource(localeId, source);
                _registeredSources[settingsKey] = source;
                _allInjectedSources.Add(source);

                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    Mod.Log.Info($"Injected {entries.Count} dictionary entries for locale '{localeId}'.");
                    Mod.Log.Info($"[Injector] Dictionary inject end key={settingsKey}, trackedSources={_allInjectedSources.Count}, registeredSources={_registeredSources.Count}.");
                }
            }
            catch (System.Exception ex)
            {
                Mod.Log.Error(ex, $"Failed to inject dictionary for locale '{localeId}'.");
            }
        }

        /// <summary>
        /// 現在のアクティブロケールIDを返す。
        /// </summary>
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
        /// 追跡中ソースを無効化し、追跡情報をクリアする。
        /// </summary>
        public static void ClearTrackedSources()
        {
            if (Mod.ModSetting?.EnableDebugLog == true)
            {
                Mod.Log.Info($"[Injector] Clear tracked sources start (trackedSources={_allInjectedSources.Count}, registeredSources={_registeredSources.Count}).");
            }

            foreach (var kvp in _registeredSources)
            {
                InvalidateSource(kvp.Value);
            }

            _registeredSources.Clear();
            _allInjectedSources.Clear();

            if (Mod.ModSetting?.EnableDebugLog == true)
            {
                Mod.Log.Info($"[Injector] Clear tracked sources end (trackedSources={_allInjectedSources.Count}, registeredSources={_registeredSources.Count}).");
            }
        }

        /// <summary>
        /// 指定オブジェクトが本Mod注入のソース参照か判定する。
        /// </summary>
        public static bool IsOurSource(object source)
        {
            if (source == null) return false;

            for (int i = 0; i < _allInjectedSources.Count; i++)
            {
                if (ReferenceEquals(source, _allInjectedSources[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 指定キーの既存ソースがあれば無効化する。
        /// </summary>
        private static void InvalidateExistingSource(string key)
        {
            if (_registeredSources.TryGetValue(key, out var existingSource))
            {
                InvalidateSource(existingSource);
                _registeredSources.Remove(key);

                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    Mod.Log.Info($"[Injector] Invalidated existing source key={key}, trackedSources={_allInjectedSources.Count}, registeredSources={_registeredSources.Count}.");
                }
            }
        }

        /// <summary>
        /// MemorySource 内部辞書を反射でクリアして無効化する。
        /// </summary>
        private static void InvalidateSource(MemorySource source)
        {
            if (source == null) return;

            try
            {
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
                {
                    Mod.Log.Warn($"Failed to invalidate MemorySource: {ex.Message}");
                }
            }
        }
    }
}
