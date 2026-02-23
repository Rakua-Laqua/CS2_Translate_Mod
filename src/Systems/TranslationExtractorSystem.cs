using CS2_Translate_Mod.Extraction;
using CS2_Translate_Mod.Models;
using Game;

namespace CS2_Translate_Mod.Systems
{
    /// <summary>
    /// 翻訳キー抽出を管理するECSシステム。
    /// 設定画面のボタンからトリガーされ、Mod別に翻訳JSONを出力する。
    /// </summary>
    public partial class TranslationExtractorSystem : GameSystemBase
    {
        /// <summary>抽出リクエストフラグ</summary>
        private static volatile bool _extractionRequested = false;

        /// <summary>最後の抽出結果</summary>
        public static ExtractionResult LastResult { get; private set; }

        /// <summary>
        /// 外部から抽出をリクエストする（設定画面のボタンから呼ばれる）。
        /// </summary>
        public static void RequestExtraction()
        {
            _extractionRequested = true;
            Mod.Log.Info("Translation extraction requested.");
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.Log.Info("TranslationExtractorSystem created.");
        }

        protected override void OnUpdate()
        {
            if (!_extractionRequested)
                return;

            _extractionRequested = false;
            PerformExtraction();
        }

        /// <summary>
        /// 抽出を実行する。
        /// </summary>
        private void PerformExtraction()
        {
            Mod.Log.Info("--- Starting translation extraction ---");

            var outputDir = Mod.TranslationsPath;
            LastResult = TranslationExtractor.ExtractAll(outputDir);

            if (LastResult.Success)
            {
                Mod.Log.Info($"--- Extraction complete: " +
                    $"{LastResult.TotalMods} mod(s), " +
                    $"{LastResult.TotalEntries} entries, " +
                    $"{LastResult.ExtractedFiles.Count} file(s) ---");

                if (LastResult.FailedMods.Count > 0)
                {
                    Mod.Log.Warn($"[Extraction] Success=true but failedMods={LastResult.FailedMods.Count}. First failed mod: {LastResult.FailedMods[0]}");
                }
            }
            else
            {
                Mod.Log.Error($"--- Extraction failed: {LastResult.ErrorMessage} ---");
                if (LastResult != null)
                {
                    Mod.Log.Error($"[Extraction] Failure detail: failedMods={LastResult.FailedMods.Count}, extractedFiles={LastResult.ExtractedFiles.Count}.");
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mod.Log.Info("TranslationExtractorSystem destroyed.");
        }
    }
}
