using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CS2_Translate_Mod.Models;
using Newtonsoft.Json;

namespace CS2_Translate_Mod.Utils
{
    /// <summary>
    /// 翻訳JSONファイルの読み込みとパースを担当するユーティリティクラス。
    /// </summary>
    public static class TranslationLoader
    {
        /// <summary>
        /// 指定ディレクトリ内のすべての翻訳JSONファイルを読み込む。
        /// サブディレクトリも再帰的に探索する。
        /// </summary>
        /// <param name="translationsDirectory">翻訳ファイルのルートディレクトリ</param>
        /// <returns>読み込みに成功した翻訳ファイルのリスト</returns>
        public static List<TranslationFile> LoadAll(string translationsDirectory)
        {
            var results = new List<TranslationFile>();

            if (!Directory.Exists(translationsDirectory))
            {
                Mod.Log.Warn($"Translations directory not found: {translationsDirectory}");
                return results;
            }

            var jsonFiles = Directory.GetFiles(translationsDirectory, "*.json", SearchOption.AllDirectories);

            Mod.Log.Info($"Found {jsonFiles.Length} translation file(s) in: {translationsDirectory}");

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var translationFile = LoadFile(filePath);
                    if (translationFile != null)
                    {
                        results.Add(translationFile);

                        if (Mod.ModSetting?.EnableDebugLog == true)
                        {
                            var translatedCount = translationFile.Entries
                                .Count(e => !string.IsNullOrEmpty(e.Value.Translation));
                            Mod.Log.Info($"  Loaded: {Path.GetFileName(filePath)} " +
                                $"(modId={translationFile.ModId}, " +
                                $"entries={translationFile.Entries.Count}, " +
                                $"translated={translatedCount})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Mod.Log.Error(ex, $"Failed to load translation file: {filePath}");
                }
            }

            return results;
        }

        /// <summary>
        /// 単一の翻訳JSONファイルを読み込む。
        /// </summary>
        /// <param name="filePath">JSONファイルのパス</param>
        /// <returns>パース結果。失敗時はnull。</returns>
        public static TranslationFile LoadFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Mod.Log.Warn($"Translation file not found: {filePath}");
                return null;
            }

            var json = File.ReadAllText(filePath);
            var translationFile = JsonConvert.DeserializeObject<TranslationFile>(json);

            if (translationFile == null)
            {
                Mod.Log.Warn($"Failed to deserialize: {filePath}");
                return null;
            }

            if (translationFile.Entries == null || translationFile.Entries.Count == 0)
            {
                Mod.Log.Warn($"No entries found in: {filePath}");
                return null;
            }

            return translationFile;
        }

        /// <summary>
        /// 複数の翻訳ファイルからローカライゼーション辞書を構築する。
        /// translationが空でないエントリのみを含む。
        /// </summary>
        /// <param name="translationFiles">翻訳ファイルのリスト</param>
        /// <returns>キー → 訳文 の辞書</returns>
        public static Dictionary<string, string> BuildDictionary(List<TranslationFile> translationFiles)
        {
            var dictionary = new Dictionary<string, string>();
            var overwriteCount = 0;

            foreach (var file in translationFiles)
            {
                if (file.Entries == null) continue;

                foreach (var entry in file.Entries)
                {
                    // 未翻訳（空文字列）のエントリはスキップ
                    if (string.IsNullOrEmpty(entry.Value.Translation))
                        continue;

                    if (dictionary.ContainsKey(entry.Key))
                    {
                        overwriteCount++;
                        if (Mod.ModSetting?.EnableDebugLog == true)
                        {
                            Mod.Log.Info($"  Overwriting key: {entry.Key} " +
                                $"(from {file.ModId})");
                        }
                    }

                    dictionary[entry.Key] = entry.Value.Translation;
                }
            }

            Mod.Log.Info($"Built translation dictionary: {dictionary.Count} entries " +
                $"({overwriteCount} overwrites)");

            return dictionary;
        }
    }
}
