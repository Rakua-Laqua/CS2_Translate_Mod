using System.Collections.Generic;
using Colossal.Localization;
using Game.SceneFlow;

namespace CS2_Translate_Mod.Localization
{
    /// <summary>
    /// ゲームのローカライゼーションシステムに翻訳を注入するクラス。
    /// MemorySource を使用して、指定ロケールに翻訳エントリを追加する。
    /// </summary>
    public static class LocalizationInjector
    {
        /// <summary>日本語ロケールID</summary>
        public const string JapaneseLocaleId = "ja-JP";

        /// <summary>現在注入中のソースを保持（再読み込み時にクリア可能）</summary>
        private static readonly List<MemorySource> _activeSources = new List<MemorySource>();

        /// <summary>
        /// 翻訳辞書をゲームのローカライゼーションシステムに注入する。
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
                var source = new MemorySource(translations);
                localizationManager.AddSource(localeId, source);
                _activeSources.Add(source);

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
                var source = new MemorySource(entries);
                localizationManager.AddSource(localeId, source);
                _activeSources.Add(source);

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
        /// アクティブソースのリストをクリアする。
        /// 注意: ゲーム側のLocalizationManagerからは削除されない。
        /// 再注入で上書きする運用を想定。
        /// </summary>
        public static void ClearTrackedSources()
        {
            _activeSources.Clear();
        }
    }
}
