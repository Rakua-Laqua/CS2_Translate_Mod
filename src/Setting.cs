using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace CS2_Translate_Mod
{
    /// <summary>
    /// Mod の設定クラス。ゲーム内オプション画面から設定可能。
    /// </summary>
    [FileLocation(nameof(CS2_Translate_Mod))]
    [SettingsUIGroupOrder(kMainGroup, kExtractionGroup)]
    [SettingsUIShowGroupName(kMainGroup, kExtractionGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kMainGroup = "Settings";
        public const string kExtractionGroup = "Extraction";

        public Setting(IMod mod) : base(mod) { }

        private bool _enableTranslation = true;
        private bool _enableDebugLog = false;

        /// <summary>翻訳ロードの有効/無効切り替え</summary>
        [SettingsUISection(kSection, kMainGroup)]
        public bool EnableTranslation
        {
            get => _enableTranslation;
            set
            {
                if (_enableTranslation == value) return;
                _enableTranslation = value;
                SaveSettingsSafe(nameof(EnableTranslation));
            }
        }

        /// <summary>デバッグログの有効/無効切り替え</summary>
        [SettingsUISection(kSection, kMainGroup)]
        public bool EnableDebugLog
        {
            get => _enableDebugLog;
            set
            {
                if (_enableDebugLog == value) return;
                _enableDebugLog = value;
                SaveSettingsSafe(nameof(EnableDebugLog));
            }
        }

        /// <summary>翻訳の再読み込みボタン</summary>
        [SettingsUISection(kSection, kMainGroup)]
        [SettingsUIButton]
        public bool ReloadTranslations
        {
            set
            {
                Mod.Log.Info("Manual reload triggered from settings.");
                Systems.TranslationLoaderSystem.RequestReload();
            }
        }

        /// <summary>Mod翻訳キー抽出ボタン</summary>
        [SettingsUISection(kSection, kExtractionGroup)]
        [SettingsUIButton]
        public bool ExtractTranslations
        {
            set
            {
                Mod.Log.Info("Extraction triggered from settings.");
                Systems.TranslationExtractorSystem.RequestExtraction();
            }
        }

        public override void SetDefaults()
        {
            _enableTranslation = true;
            _enableDebugLog = false;
        }

        private void SaveSettingsSafe(string reason)
        {
            try
            {
                ApplyAndSave();
                if (_enableDebugLog)
                {
                    Mod.Log.Info($"Settings saved ({reason}).");
                }
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"Failed to save settings ({reason}): {ex.Message}");
            }
        }

        /// <summary>
        /// Mod のオプション画面用ローカライズを提供する。
        /// CS2 は {Assembly}.{ModFullType} 形式のキーを生成するため、それに合わせる。
        /// </summary>
        public void RegisterLocalization()
        {
            // CS2 が生成するキー形式: {AssemblyName}.{Namespace}.{ModClassName}
            // 例: CS2_Translate_Mod.CS2_Translate_Mod.Mod
            var modType = typeof(Mod);
            var modId = $"{modType.Assembly.GetName().Name}.{modType.FullName}";
            var st = nameof(Setting);

            var jaEntries = new Dictionary<string, string>
            {
                // セクション・タブ・グループ
                { $"Options.SECTION[{modId}]", "Mod翻訳" },
                { $"Options.TAB[{modId}.{kSection}]", "メイン" },
                { $"Options.GROUP[{modId}.{kMainGroup}]", "設定" },
                { $"Options.GROUP[{modId}.{kExtractionGroup}]", "抽出" },

                // EnableTranslation
                { $"Options.OPTION[{modId}.{st}.{nameof(EnableTranslation)}]", "翻訳を有効にする" },
                { $"Options.OPTION_DESCRIPTION[{modId}.{st}.{nameof(EnableTranslation)}]", "Modの日本語翻訳の読み込みを有効または無効にします。" },

                // EnableDebugLog
                { $"Options.OPTION[{modId}.{st}.{nameof(EnableDebugLog)}]", "デバッグログを有効にする" },
                { $"Options.OPTION_DESCRIPTION[{modId}.{st}.{nameof(EnableDebugLog)}]", "翻訳の読み込み状況の詳細ログを出力します。" },

                // ReloadTranslations
                { $"Options.OPTION[{modId}.{st}.{nameof(ReloadTranslations)}]", "翻訳を再読み込み" },
                { $"Options.OPTION_DESCRIPTION[{modId}.{st}.{nameof(ReloadTranslations)}]", "翻訳JSONファイルを再読み込みして適用します。" },

                // ExtractTranslations
                { $"Options.OPTION[{modId}.{st}.{nameof(ExtractTranslations)}]", "翻訳キーを抽出" },
                { $"Options.OPTION_DESCRIPTION[{modId}.{st}.{nameof(ExtractTranslations)}]", "インストール済みModのローカライゼーションキーをMod別に抽出し、翻訳JSONファイルとして出力します。" },
            };

            // 英語フォールバック
            var enEntries = new Dictionary<string, string>
            {
                { $"Options.SECTION[{modId}]", "Mod Translation" },
                { $"Options.TAB[{modId}.{kSection}]", "Main" },
                { $"Options.GROUP[{modId}.{kMainGroup}]", "Settings" },
                { $"Options.GROUP[{modId}.{kExtractionGroup}]", "Extraction" },
                { $"Options.OPTION[{modId}.{st}.{nameof(EnableTranslation)}]", "Enable Translation" },
                { $"Options.OPTION_DESCRIPTION[{modId}.{st}.{nameof(EnableTranslation)}]", "Enable or disable Japanese translation loading for mods." },
                { $"Options.OPTION[{modId}.{st}.{nameof(EnableDebugLog)}]", "Enable Debug Log" },
                { $"Options.OPTION_DESCRIPTION[{modId}.{st}.{nameof(EnableDebugLog)}]", "Output detailed logs for translation loading." },
                { $"Options.OPTION[{modId}.{st}.{nameof(ReloadTranslations)}]", "Reload Translations" },
                { $"Options.OPTION_DESCRIPTION[{modId}.{st}.{nameof(ReloadTranslations)}]", "Reload and apply translation JSON files." },
                { $"Options.OPTION[{modId}.{st}.{nameof(ExtractTranslations)}]", "Extract Translation Keys" },
                { $"Options.OPTION_DESCRIPTION[{modId}.{st}.{nameof(ExtractTranslations)}]", "Extract localization keys from installed mods and output as translation JSON files." },
            };

            Localization.LocalizationInjector.InjectDictionary("ja-JP", jaEntries);
            Localization.LocalizationInjector.InjectDictionary("en-US", enEntries);
        }
    }
}
