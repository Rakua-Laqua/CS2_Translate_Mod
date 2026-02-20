# CHANGELOG.md

## v1.1.3 (2026-02-21)

### バグ修正: 自身の注入MemorySourceが他Modの抽出を妨害する問題の修正
- Phase 1 で自身が注入したMemorySource（2988エントリ、全翻訳ファイルの合算）をスキップするロジックを追加
  - このソースが `ExtendedTooltip` と誤判定され、C2VM等のキーが吸収されていた
  - C2VMのTrafficLightsEnhancementが抽出されなかった原因
- `LocalizationInjector`
  - `_allInjectedSources`: 全注入MemorySourceの参照を追跡するListを追加
  - `IsOurSource()`: ReferenceEqualsで自身注入ソースを判定するpublicメソッドを追加
- `TranslationExtractor` Phase 1
  - ReadEntries前に `LocalizationInjector.IsOurSource()` で判定し SELF-INJECT としてスキップ
  - Phase 1.5 の `uncollected` キーからC2VMが正常に新規Modとして抽出されるように

## v1.1.2 (2026-02-20)

### バグ修正: バニラ設定カテゴリがModとして抽出される問題の修正
- ブラケット内から抽出したMod IDを `StandardCategories` で検証するロジックを追加
  - `Options.OPTION[GraphicsSettings.VSync]` → "GraphicsSettings" がバニラと判定されスキップされるように
  - `ExtractBestModIdFromKeys`, `ExtractModIdStrict`, `ExtractModId` の3箇所で一貫して適用
- Phase 1.5 で `GraphicsSettings`(32), `InputSettings`(477), `Gamepad`(150) 等約34個のバニラグループが作成されていた問題を解決

## v1.1.1 (2026-02-20)

### バグ修正: バニラキーのModファイルへの混入を修正
- `ExtractBestModIdFromKeys` に `bracketOnly` パラメータを追加
  - ゲームアセンブリ由来ソース（汎用型・通常型）でブラケットパターン限定モードを適用
  - ドット区切りフォールバックが `Statistics.XXX`, `Tutorials.XXX` 等のバニラキーをModと誤認していた問題を解決
- Phase 1.5 の `SupplementFromActiveDictionary` で `ExtractModIdStrict`（ブラケットのみ）を使用
  - `Statistics`, `Tutorials`, `Progression`, `Glossary` 等のバニラカテゴリがModとして抽出される問題を解決
- `VanillaPrefixes` を大幅拡充（約70カテゴリ追加）
  - 情報パネル系（CityInfoPanel, ElectricityInfoPanel 等）
  - 品質設定系（AnimationQualitySettings, SSAOQualitySettings 等）
  - 入力系（Keyboard, Mouse, XBOX, PS 等）
  - ゲームプレイ系（Statistics, Progression, Glossary, Traffic 等）
- `StandardCategories` も同様に拡充

## v1.1.0 (2026-02-20)

### 機能改善: 翻訳抽出ロジックの大幅強化
- `IdentifyModFromSource` をアセンブリ優先方式に変更
  - Modアセンブリ由来のソースは無条件でMod扱い（旧: キー分析最優先でVanillaPrefixesに誤除外されるケースあり）
  - ゲームアセンブリの汎用ソース型（MemorySource等）はキー分析でMod判定する新ロジックを追加
- Phase 1.5 補完抽出（`SupplementFromActiveDictionary`）を新設
  - Phase 1 で漏れたModキーを activeDictionary から補完
  - 既存Modグループとの正規化名マッチングで統合 or 新規グループ作成
- `ModIdPatterns[]` 複数パターン対応
  - 旧: 単一正規表現 → 新: 3パターン順次試行
  - `YY_TREE_CONTROLLER[radius]` 等のアンダースコア区切りパターンを捕捉可能に
- ソースコンテキスト別キー分析の導入
  - Modソース用（VanillaFilter無し）、汎用ソース用（Filter有り）、ゲームソース用の3種類に分岐

### ログ改善
- `ReadEntries` 失敗時に常にWarnログ出力（旧: EnableDebugLog有効時のみ）
- 各ソースの型名・アセンブリ名・ロケール・エントリ数を常時記録
- Phase 1/1.5 の統計サマリー（vanilla/empty/self/readErrorの内訳）出力

### その他
- サンプル翻訳JSON (`_sample_Anarchy.json`) を削除
- 抽出済み翻訳ファイルを `Translations.zip` に整理

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
