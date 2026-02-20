using System;
using System.IO;
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
        public static bool SuppressLocaleCallback { get; set; } = false;

        /// <summary>デバウンス用: 最後の locale 変更時刻 (UTC ticks)</summary>
        public static long LastLocaleChangeTicks { get; private set; } = 0;

        /// <summary>デバウンス用: 未処理の locale 変更があるか</summary>
        public static bool LocaleChangePending { get; set; } = false;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("=== CS2_Translate_Mod: Loading ===");

            try
            {
                // Mod ディレクトリの取得
                // CS2 では Assembly.Location/CodeBase が信頼できないため、
                // Unity の persistentDataPath を基にパスを構築する
                var persistentData = Application.persistentDataPath;
                Log.Info($"Application.persistentDataPath: '{persistentData}'");

                ModPath = Path.Combine(persistentData, "Mods", "CS2_Translate_Mod");
                TranslationsPath = Path.Combine(ModPath, "Translations");

                Log.Info($"Mod Path: {ModPath}");
                Log.Info($"Translations Path: {TranslationsPath}");

                // 翻訳ディレクトリが無ければ作成
                if (!Directory.Exists(TranslationsPath))
                {
                    Directory.CreateDirectory(TranslationsPath);
                    Log.Info("Created Translations directory.");
                }

                // Mod 設定の初期化
                Log.Info("Initializing settings...");
                ModSetting = new Setting(this);
                ModSetting.RegisterInOptionsUI();
                GameManager.instance.localizationManager.onActiveDictionaryChanged += OnLocaleChanged;
                Log.Info("Settings registered.");

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

                // 翻訳読み込みシステムを登録
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
        /// ロケール変更時に呼ばれるコールバック。
        /// ロケール変更後に翻訳を再注入する。
        /// </summary>
        private void OnLocaleChanged()
        {
            // 翻訳注入中の再帰呼び出しを抑制
            if (SuppressLocaleCallback) return;

            // デバウンス: タイムスタンプだけ更新し、実際の再読み込みはシステム側で遅延処理
            LastLocaleChangeTicks = DateTime.UtcNow.Ticks;
            LocaleChangePending = true;
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
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during disposal.");
            }
        }
    }
}
