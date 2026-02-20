# CHANGELOG.md

## v1.0.1 (2026-02-20)

### セキュリティ修正
- `SanitizeModId` のパストラバーサル脆弱性を修正（`..` が通過する問題）
- `WriteModTranslationFile` に `Path.GetFullPath` による出力先ディレクトリ検証を追加

### バグ修正
- `IsStandardCategory` が呼び出しごとに `HashSet` を生成していた問題を修正（`static readonly` に昇格）
- 静的フラグ（`LastLocaleChangeTicks`, `SuppressLocaleCallback`, `LocaleChangePending`）のスレッドセーフティ欠如を修正（`Interlocked` / `volatile`）
- `MemorySource` が再注入のたびに無限蓄積するメモリリークを修正（旧ソースの内部辞書クリア方式に変更）
- `TranslationLoader.BuildDictionary` で `entry.Value` が null の場合に `NullReferenceException` が発生する問題を修正
- `ExtractEntriesFromObject` の再帰的リフレクション探索で循環参照による無限ループ/指数的爆発を防止（訪問済みオブジェクト追跡を追加）
- `WriteModTranslationFile` で既存翻訳ファイル破損時にデータが消失する問題を修正（`.bak` バックアップを作成）
- `TranslationExtractorSystem`, `TranslationLoaderSystem` の static フラグに `volatile` を追加

### パフォーマンス改善
- `VanillaPrefixes` を `HashSet` から `string[]` に変更（`StartsWith` で使うためデータ構造の意図を一致）
- `GetLocalePriority` の `ToLowerInvariant()` を `StringComparison.OrdinalIgnoreCase` に変更（不要なアロケーション削減）
- `NormalizeModName` の `Replace` チェーンを `StringBuilder` に変更
- `SanitizeModId` の LINQ アロケーションを `StringBuilder` に変更
- `IdentifyModFromKeys` の `OrderByDescending().First()` を O(n) ループに変更
- `TranslationLoader.BuildDictionary` の `ContainsKey` → `TryGetValue` で二重ルックアップ排除

### アーキテクチャ改善
- `ExtractionResult` クラスを `TranslationExtractor.cs` 内から `Models/ExtractionResult.cs` に分離
- 翻訳ファイル読み込み順を `Array.Sort` で決定的に（再現性確保）
- 翻訳ファイルサイズ上限 (50MB) とファイル数上限 (500) を追加（DoS対策）
- 例外の握りつぶし（空 `catch`）にデバッグモード時のログ出力を追加
- `File.ReadAllText` / `WriteAllText` にUTF-8エンコーディングを明示
- `DateTime.Now` → `DateTime.UtcNow` に変更（タイムゾーン依存の回避）

## v1.0.0

- 初回リリース
