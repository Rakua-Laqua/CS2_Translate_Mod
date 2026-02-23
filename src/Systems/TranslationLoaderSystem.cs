using System;
using System.Collections.Generic;
using CS2_Translate_Mod.Localization;
using CS2_Translate_Mod.Utils;
using Game;
using Game.SceneFlow;

namespace CS2_Translate_Mod.Systems
{
    /// <summary>
    /// 翻訳ファイルの読み込みとローカライゼーション注入を担当する ECS システム。
    /// </summary>
    public partial class TranslationLoaderSystem : GameSystemBase
    {
        /// <summary>翻訳が既に読み込み済みかどうか</summary>
        private bool _loaded = false;

        /// <summary>手動再読み込みリクエストフラグ</summary>
        private static volatile bool _reloadRequested = false;

        /// <summary>読み込んだ翻訳エントリの総数</summary>
        private int _totalEntries = 0;

        /// <summary>読み込んだ翻訳ファイルの総数</summary>
        private int _totalFiles = 0;

        /// <summary>ロケール変更イベントのデバウンス秒数</summary>
        private const float DebounceSeconds = 3.0f;

        #region Optimization flags (Step1 & Step2) — false にすれば無効化される
        /// <summary>[Step1] true: イベント世代番号で処理済みイベントの再処理をスキップ</summary>
        private static readonly bool EnableEventGenerationFilter = true;
        /// <summary>[Step2] true: 辞書フィンガープリントが同一なら注入をスキップ</summary>
        private static readonly bool EnableFingerprintSkip = true;
        #endregion

        /// <summary>[Step1] 最後に処理したイベント世代番号</summary>
        private long _lastProcessedGeneration = 0;

        /// <summary>[Step2] 最後に注入した辞書のフィンガープリント</summary>
        private string _lastDictionaryFingerprint = null;

        /// <summary>
        /// 外部から再読み込みをリクエストする（設定画面のボタンなど）。
        /// </summary>
        public static void RequestReload()
        {
            _reloadRequested = true;
            Mod.Log.Info("Translation reload requested (manual).");
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.Log.Info("TranslationLoaderSystem created.");
        }

        protected override void OnUpdate()
        {
            if (!_loaded)
            {
                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    Mod.Log.Info($"[ReloadDiag] Initial update start (pending={Mod.LocaleChangePending}, lastChangeTicks={Mod.LastLocaleChangeTicks}, callbackCount={Mod.LocaleCallbackCount}, eventGen={Mod.EventGeneration}).");
                }

                LoadAndInjectTranslations("initial_on_update");
                _loaded = true;
                _lastProcessedGeneration = Mod.EventGeneration;
            }

            if (_reloadRequested)
            {
                _reloadRequested = false;

                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    Mod.Log.Info($"[ReloadDiag] Manual reload accepted (pending={Mod.LocaleChangePending}, callbackCount={Mod.LocaleCallbackCount}, eventGen={Mod.EventGeneration}).");
                }

                LoadAndInjectTranslations("manual_request");
                _lastProcessedGeneration = Mod.EventGeneration;
            }

            if (Mod.LocaleChangePending)
            {
                var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Mod.LastLocaleChangeTicks);
                if (elapsed.TotalSeconds >= DebounceSeconds)
                {
                    var lastChangeTicks = Mod.LastLocaleChangeTicks;
                    var lastChangeUtc = lastChangeTicks > 0
                        ? new DateTime(lastChangeTicks, DateTimeKind.Utc).ToString("O")
                        : "<unset>";

                    Mod.LocaleChangePending = false;

                    var currentGen = Mod.EventGeneration;
                    if (EnableEventGenerationFilter && currentGen <= _lastProcessedGeneration)
                    {
                        Mod.Log.Info($"[Optimization/Step1] Debounced reload skipped — no new events (eventGen={currentGen}, lastProcessed={_lastProcessedGeneration}, waited {elapsed.TotalSeconds:F1}s, callbackCount={Mod.LocaleCallbackCount}).");
                    }
                    else
                    {
                        Mod.Log.Info($"Locale change debounced - reloading translations (waited {elapsed.TotalSeconds:F1}s, lastChangeUtc={lastChangeUtc}, eventGen={currentGen}, lastProcessed={_lastProcessedGeneration}, callbackCount={Mod.LocaleCallbackCount}).");
                        LoadAndInjectTranslations("locale_changed_debounced");
                        _lastProcessedGeneration = currentGen;
                    }
                }
            }
        }

        /// <summary>
        /// 翻訳ファイルを読み込み、ゲームのローカライゼーションシステムに注入する。
        /// </summary>
        private void LoadAndInjectTranslations(string trigger)
        {
            if (Mod.ModSetting != null && !Mod.ModSetting.EnableTranslation)
            {
                Mod.Log.Info($"Translation loading is disabled in settings (trigger={trigger}).");

                if (Mod.ModSetting?.EnableDebugLog == true)
                {
                    Mod.Log.Info($"[ReloadDiag] Skip details: pending={Mod.LocaleChangePending}, callbackCount={Mod.LocaleCallbackCount}, trackedSources={LocalizationInjector.GetTrackedSourceCount()}, registeredSources={LocalizationInjector.GetRegisteredSourceCount()}.");
                }

                return;
            }

            Mod.Log.Info($"--- Loading translations (trigger={trigger}) ---");

            var translationFiles = TranslationLoader.LoadAll(Mod.TranslationsPath);

            if (translationFiles.Count == 0)
            {
                Mod.Log.Info("No translation files found. " +
                    $"Place JSON files in: {Mod.TranslationsPath}");
                return;
            }

            _totalFiles = translationFiles.Count;

            var dictionary = TranslationLoader.BuildDictionary(translationFiles);
            _totalEntries = dictionary.Count;

            if (dictionary.Count == 0)
            {
                Mod.Log.Info("No translated entries found in loaded files.");
                return;
            }

            // [Optimization Step2] フィンガープリント比較で内容未変更なら注入スキップ
            string fingerprint = EnableFingerprintSkip ? ComputeDictionaryFingerprint(dictionary) : null;
            if (EnableFingerprintSkip && trigger != "manual_request" && fingerprint == _lastDictionaryFingerprint)
            {
                Mod.Log.Info($"[Optimization/Step2] Dictionary unchanged (fingerprint={fingerprint}, trigger={trigger}), skipping injection.");
                return;
            }

            Mod.SuppressLocaleCallback = true;
            bool success;
            try
            {
                success = LocalizationInjector.InjectJapanese(dictionary);
            }
            finally
            {
                Mod.SuppressLocaleCallback = false;
            }

            if (success)
            {
                Mod.Log.Info($"--- Translation loading complete (trigger={trigger}): " +
                    $"{_totalFiles} file(s), {_totalEntries} entries ---");

                // フィンガープリントを成功時のみ更新
                if (fingerprint != null)
                {
                    _lastDictionaryFingerprint = fingerprint;
                }

                Mod.SuppressLocaleCallback = true;
                try
                {
                    Mod.ModSetting?.RegisterLocalization();
                }
                finally
                {
                    Mod.SuppressLocaleCallback = false;
                }
            }
            else
            {
                Mod.Log.Error($"--- Translation loading failed (trigger={trigger}) ---");
            }

            if (Mod.ModSetting?.EnableDebugLog == true)
            {
                var activeLocale = LocalizationInjector.GetActiveLocaleId();
                Mod.Log.Info($"[ReloadDiag] Post-load: trigger={trigger}, activeLocale={activeLocale}, callbackCount={Mod.LocaleCallbackCount}, eventGen={Mod.EventGeneration}, fingerprint={_lastDictionaryFingerprint ?? "<none>"}, trackedSources={LocalizationInjector.GetTrackedSourceCount()}, registeredSources={LocalizationInjector.GetRegisteredSourceCount()}.");
            }
        }

        /// <summary>
        /// [Optimization Step2] 辞書のフィンガープリントを計算する（反復順序非依存）。
        /// </summary>
        private static string ComputeDictionaryFingerprint(Dictionary<string, string> dictionary)
        {
            unchecked
            {
                long hash = 17;
                foreach (var kvp in dictionary)
                {
                    // XOR で反復順序に依存しないハッシュを生成
                    hash ^= ((long)(kvp.Key?.GetHashCode() ?? 0) * 397) ^ (kvp.Value?.GetHashCode() ?? 0);
                }
                return $"{dictionary.Count}:{hash:X16}";
            }
        }

        protected override void OnDestroy()
        {
            _lastDictionaryFingerprint = null;
            LocalizationInjector.ClearTrackedSources();
            base.OnDestroy();
            Mod.Log.Info("TranslationLoaderSystem destroyed.");
        }
    }
}
