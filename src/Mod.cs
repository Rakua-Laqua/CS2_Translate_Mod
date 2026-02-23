using System;
using System.IO;
using System.Threading;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using UnityEngine;

namespace CS2_Translate_Mod
{
    /// <summary>
    /// Cities: Skylines 2 Mod日本語化Mod のエントリポイント。
    /// 翻訳JSONファイルを読み込み、ゲームのローカライゼーションシステムに注入する。
    /// </summary>
    public class Mod : IMod
    {
        public static readonly ILog Log = LogManager.GetLogger(nameof(CS2_Translate_Mod))
            .SetShowsErrorsInUI(false);

        /// <summary>Mod のアセンブリが格納されているディレクトリパス</summary>
        public static string ModPath { get; private set; }

        /// <summary>翻訳JSONファイルを配置するディレクトリパス</summary>
        public static string TranslationsPath { get; private set; }

        /// <summary>Mod 設定</summary>
        public static Setting ModSetting { get; private set; }

        /// <summary>翻訳注入中のコールバック抑制フラグ</summary>
        public static volatile bool SuppressLocaleCallback = false;

        /// <summary>デバウンス用: 最後の locale 変更時刻 (UTC ticks)</summary>
        private static long _lastLocaleChangeTicks = 0;
        public static long LastLocaleChangeTicks
        {
            get => Interlocked.Read(ref _lastLocaleChangeTicks);
            private set => Interlocked.Exchange(ref _lastLocaleChangeTicks, value);
        }

        /// <summary>デバウンス用: 未処理の locale 変更があるか</summary>
        public static volatile bool LocaleChangePending = false;

        /// <summary>診断用: locale 変更コールバックの累計呼び出し回数</summary>
        private static long _localeCallbackCount = 0;
        public static long LocaleCallbackCount => Interlocked.Read(ref _localeCallbackCount);

        /// <summary>[Optimization Step1] イベント世代番号: 非抑制ロケール変更ごとにインクリメント</summary>
        private static long _eventGeneration = 0;
        public static long EventGeneration => Interlocked.Read(ref _eventGeneration);

        public static void ResetLocaleChangeState(string reason)
        {
            LastLocaleChangeTicks = 0;
            LocaleChangePending = false;
            Interlocked.Exchange(ref _localeCallbackCount, 0);
            Interlocked.Exchange(ref _eventGeneration, 0);
            Log.Info($"Locale change state reset ({reason}).");
        }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("=== CS2_Translate_Mod: Loading ===");

            try
            {
                // CS2 では Assembly.Location/CodeBase が信頼できないため、
                // Unity の persistentDataPath を基にパスを構築する。
                var persistentData = Application.persistentDataPath;
                Log.Info($"Application.persistentDataPath: '{persistentData}'");

                ModPath = Path.Combine(persistentData, "Mods", "CS2_Translate_Mod");
                TranslationsPath = Path.Combine(ModPath, "Translations");

                Log.Info($"Mod Path: {ModPath}");
                Log.Info($"Translations Path: {TranslationsPath}");

                if (!Directory.Exists(TranslationsPath))
                {
                    Directory.CreateDirectory(TranslationsPath);
                    Log.Info("Created Translations directory.");
                }

                Log.Info("Initializing settings...");
                ModSetting = new Setting(this);
                ModSetting.RegisterInOptionsUI();
                AssetDatabase.global.LoadSettings(nameof(CS2_Translate_Mod), ModSetting, new Setting(this));
                ResetLocaleChangeState("OnLoad");
                GameManager.instance.localizationManager.onActiveDictionaryChanged += OnLocaleChanged;
                Log.Info($"Settings registered. EnableTranslation={ModSetting.EnableTranslation}, EnableDebugLog={ModSetting.EnableDebugLog}");

                // 設定画面のローカライズを即座に登録
                SuppressLocaleCallback = true;
                try
                {
                    ModSetting.RegisterLocalization();
                    Log.Info("Settings localization registered.");
                }
                finally
                {
                    SuppressLocaleCallback = false;
                }

                Log.Info("Registering systems...");
                updateSystem.UpdateAt<Systems.TranslationLoaderSystem>(SystemUpdatePhase.MainLoop);
                updateSystem.UpdateAt<Systems.TranslationExtractorSystem>(SystemUpdatePhase.MainLoop);
                Log.Info("Systems registered.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "!!! CS2_Translate_Mod: FATAL ERROR in OnLoad !!!");
            }

            Log.Info("=== CS2_Translate_Mod: Loaded ===");
        }

        /// <summary>
        /// ロケール辞書変更イベントのコールバック。
        /// 実処理は TranslationLoaderSystem 側でデバウンス後に行う。
        /// </summary>
        private void OnLocaleChanged()
        {
            var callbackCount = Interlocked.Increment(ref _localeCallbackCount);
            var nowTicks = DateTime.UtcNow.Ticks;
            var previousTicks = LastLocaleChangeTicks;
            var pendingBefore = LocaleChangePending;
            var debugLogEnabled = ModSetting?.EnableDebugLog == true;

            if (debugLogEnabled)
            {
                var elapsedMs = previousTicks > 0
                    ? (nowTicks - previousTicks) / TimeSpan.TicksPerMillisecond
                    : -1;
                var activeLocale = GameManager.instance?.localizationManager?.activeLocaleId ?? "<unknown>";
                Log.Info($"[LocaleEvent] #{callbackCount} received (suppress={SuppressLocaleCallback}, pendingBefore={pendingBefore}, activeLocale={activeLocale}, msSincePrev={elapsedMs}).");
            }

            if (SuppressLocaleCallback)
            {
                if (debugLogEnabled)
                {
                    Log.Info($"[LocaleEvent] #{callbackCount} suppressed.");
                }
                return;
            }

            LastLocaleChangeTicks = nowTicks;
            LocaleChangePending = true;
            var gen = Interlocked.Increment(ref _eventGeneration);

            if (debugLogEnabled)
            {
                Log.Info($"[LocaleEvent] #{callbackCount} marked pending (lastChangeTicks={LastLocaleChangeTicks}, eventGen={gen}).");
            }
        }

        public void OnDispose()
        {
            Log.Info("=== CS2_Translate_Mod: Disposing ===");

            try
            {
                if (GameManager.instance?.localizationManager != null)
                {
                    GameManager.instance.localizationManager.onActiveDictionaryChanged -= OnLocaleChanged;
                }

                if (ModSetting != null)
                {
                    ModSetting.UnregisterInOptionsUI();
                    ModSetting = null;
                }

                Log.Info($"Locale callback total count: {LocaleCallbackCount}");
                ResetLocaleChangeState("OnDispose");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during disposal.");
            }
        }
    }
}
