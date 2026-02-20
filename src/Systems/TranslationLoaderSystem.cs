using System;
using System.Collections.Generic;
using CS2_Translate_Mod.Localization;
using CS2_Translate_Mod.Utils;
using Game;
using Game.SceneFlow;

namespace CS2_Translate_Mod.Systems
{
    /// <summary>
    /// 翻訳ファイルの読み込みとローカライゼーション注入を管理するECSシステム。
    /// ゲーム起動時に自動実行され、翻訳JSONファイルをロードして日本語ロケールに注入する。
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

        /// <summary>デバウンス遅延 (秒)</summary>
        private const float DebounceSeconds = 3.0f;

        /// <summary>
        /// 外部から再読み込みをリクエストする（設定画面のボタン等から呼ばれる）。
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
            // 初回ロード
            if (!_loaded)
            {
                LoadAndInjectTranslations();
                _loaded = true;
            }

            // 手動再読み込みリクエスト（即時処理）
            if (_reloadRequested)
            {
                _reloadRequested = false;
                LoadAndInjectTranslations();
            }

            // Locale 変更のデバウンス処理
            // 最後のイベントから DebounceSeconds 秒経過してから1回だけ処理する
            if (Mod.LocaleChangePending)
            {
                var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Mod.LastLocaleChangeTicks);
                if (elapsed.TotalSeconds >= DebounceSeconds)
                {
                    Mod.LocaleChangePending = false;
                    Mod.Log.Info($"Locale change debounced — reloading translations (waited {elapsed.TotalSeconds:F1}s).");
                    LoadAndInjectTranslations();
                }
            }
        }

        /// <summary>
        /// 翻訳ファイルを読み込み、ゲームのローカライゼーションシステムに注入する。
        /// </summary>
        private void LoadAndInjectTranslations()
        {
            // 設定で無効化されている場合はスキップ
            if (Mod.ModSetting != null && !Mod.ModSetting.EnableTranslation)
            {
                Mod.Log.Info("Translation loading is disabled in settings.");
                return;
            }

            Mod.Log.Info("--- Loading translations ---");

            // 1. 翻訳ファイルを読み込む
            var translationFiles = TranslationLoader.LoadAll(Mod.TranslationsPath);

            if (translationFiles.Count == 0)
            {
                Mod.Log.Info("No translation files found. " +
                    $"Place JSON files in: {Mod.TranslationsPath}");
                return;
            }

            _totalFiles = translationFiles.Count;

            // 2. 辞書を構築
            var dictionary = TranslationLoader.BuildDictionary(translationFiles);
            _totalEntries = dictionary.Count;

            if (dictionary.Count == 0)
            {
                Mod.Log.Info("No translated entries found in loaded files.");
                return;
            }

            // 3. 日本語ロケールに注入（コールバック抑制付き）
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
                Mod.Log.Info($"--- Translation loading complete: " +
                    $"{_totalFiles} file(s), {_totalEntries} entries ---");

                // Mod自体の設定画面ローカライズも注入（コールバック抑制付き）
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
                Mod.Log.Error("--- Translation loading failed ---");
            }

            // デバッグログ: 現在のロケール情報
            if (Mod.ModSetting?.EnableDebugLog == true)
            {
                var activeLocale = LocalizationInjector.GetActiveLocaleId();
                Mod.Log.Info($"Active locale: {activeLocale}");
            }
        }

        protected override void OnDestroy()
        {
            LocalizationInjector.ClearTrackedSources();
            base.OnDestroy();
            Mod.Log.Info("TranslationLoaderSystem destroyed.");
        }
    }
}
