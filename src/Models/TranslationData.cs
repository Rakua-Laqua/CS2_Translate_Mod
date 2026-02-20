using System.Collections.Generic;
using Newtonsoft.Json;

namespace CS2_Translate_Mod.Models
{
    /// <summary>
    /// 翻訳JSONファイルのルートオブジェクト。
    /// </summary>
    /// <example>
    /// {
    ///   "modId": "_extracted_mod_Anarchy",
    ///   "modName": "Extracted from mod_Anarchy",
    ///   "version": "2026-02-19",
    ///   "entries": {
    ///     "Options.SECTION[Anarchy.Anarchy.AnarchyMod]": {
    ///       "original": "Anarchy",
    ///       "translation": "アナーキー"
    ///     }
    ///   }
    /// }
    /// </example>
    public class TranslationFile
    {
        /// <summary>Mod の識別子</summary>
        [JsonProperty("modId")]
        public string ModId { get; set; }

        /// <summary>Mod の名称</summary>
        [JsonProperty("modName")]
        public string ModName { get; set; }

        /// <summary>翻訳ファイルのバージョン（日付文字列等）</summary>
        [JsonProperty("version")]
        public string Version { get; set; }

        /// <summary>翻訳エントリ（キー → エントリデータ）</summary>
        [JsonProperty("entries")]
        public Dictionary<string, TranslationEntry> Entries { get; set; }
            = new Dictionary<string, TranslationEntry>();
    }

    /// <summary>
    /// 翻訳エントリの1項目。
    /// </summary>
    public class TranslationEntry
    {
        /// <summary>原文（英語）</summary>
        [JsonProperty("original")]
        public string Original { get; set; }

        /// <summary>訳文（日本語）。空文字列の場合は未翻訳。</summary>
        [JsonProperty("translation")]
        public string Translation { get; set; }
    }
}
