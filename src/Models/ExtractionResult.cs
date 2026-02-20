using System.Collections.Generic;

namespace CS2_Translate_Mod.Models
{
    /// <summary>
    /// 抽出結果のサマリー。
    /// </summary>
    public class ExtractionResult
    {
        public int TotalMods { get; set; }
        public int TotalEntries { get; set; }
        public List<string> ExtractedFiles { get; set; } = new List<string>();
        public List<string> FailedMods { get; set; } = new List<string>();
        public string ErrorMessage { get; set; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage) && ExtractedFiles.Count > 0;
    }
}
