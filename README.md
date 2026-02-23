# CS2_Translate_Mod

Cities: Skylines 2 の Mod を翻訳するための翻訳ローダー＆抽出 Mod です。  
翻訳 JSON ファイルを `Translations` フォルダに配置するだけで、他の Mod の UI を任意の言語に翻訳できます。  
デフォルトでは日本語 (`ja-JP`) への注入を行いますが、仕組み上はどのロケールにも対応可能です。  
また、インストール済み Mod のローカライゼーションキーを **Mod ごとに多段階方式で自動抽出** して翻訳テンプレートを生成できます。

---

## 機能

### 翻訳読み込み・注入

- **翻訳JSONの自動読み込み** — `Translations` フォルダ内の `.json` ファイルを自動検出・読み込み（サブディレクトリも再帰探索）
- **ロケールへの注入** — ゲームのローカライゼーションシステムに翻訳を注入（デフォルト: `ja-JP`、任意のロケールIDに対応可能）
- **複数Mod対応** — 複数の翻訳ファイルを同時に読み込み可能（最大500ファイル、各50MBまで）
- **ロケール変更時の自動再注入** — ゲーム言語切り替え時にデバウンス（3秒）付きで翻訳を自動再適用
- **再読み込み機能** — ゲーム内設定画面から翻訳を手動で再読み込み

### 翻訳キー抽出

- **多段階Mod別キー抽出** — インストール済みModのローカライゼーションキーを3段階の方式で抽出
  - **Phase 1**: `m_UserSources` からソースのアセンブリ情報でMod別にグループ化（推奨・高精度）
  - **Phase 1.5**: `activeDictionary` から Phase 1 で漏れたModキーを補完抽出
  - **Phase 2**: フォールバックとして `activeDictionary` の全エントリをキーパターンでグルーピング
- **バニラキー自動除外** — ゲーム本体のローカライゼーションキーを自動判定し除外（約70カテゴリ対応）
- **自己注入ソース除外** — 本Mod自身が注入した翻訳ソースを抽出対象から自動除外
- **既訳マージ** — 抽出時、既存の翻訳ファイルがあれば既訳を保持してマージ（破損時は `.bak` バックアップ作成）

### 最適化・安全性

- **イベント世代フィルタ** — 処理済みロケール変更イベントの重複処理をスキップ
- **辞書フィンガープリント** — 翻訳内容が前回と同一の場合、再注入をスキップ
- **コールバック抑制** — 翻訳注入中のロケール変更コールバックを抑制し、再帰的ループを防止
- **デバッグログ** — 翻訳の読み込み・抽出・注入状況を詳細に確認可能

---

## プロジェクト構成

```
CS2_Translate_Mod/
├── src/                                  # ソースコード
│   ├── Mod.cs                            # エントリポイント (IMod 実装)
│   ├── Setting.cs                        # ゲーム内設定 (オプションUI)
│   ├── Models/
│   │   ├── TranslationData.cs            # 翻訳JSON データモデル
│   │   └── ExtractionResult.cs           # 抽出結果サマリーモデル
│   ├── Utils/
│   │   └── TranslationLoader.cs          # JSONファイル読み込み・パース・辞書構築
│   ├── Localization/
│   │   └── LocalizationInjector.cs       # ローカライゼーション注入・ソース管理
│   ├── Extraction/
│   │   └── TranslationExtractor.cs       # 多段階Mod別翻訳キー抽出
│   └── Systems/
│       ├── TranslationLoaderSystem.cs     # ECSシステム（読み込み・注入制御・最適化）
│       └── TranslationExtractorSystem.cs  # ECSシステム（抽出制御）
├── Translations/                         # 翻訳JSONファイル配置先
├── docs/
│   └── TROUBLESHOOTING.md                # トラブルシューティング記録
├── CHANGELOG.md                          # 変更履歴
├── CS2_Translate_Mod.csproj              # プロジェクトファイル
├── CS2_Translate_Mod.sln                 # ソリューションファイル
├── Directory.Build.props.template        # ゲームパス設定テンプレート
├── README.md
└── LICENSE
```

---

## セットアップ

### 1. ゲームパスの設定

`Directory.Build.props.template` をコピーして `Directory.Build.props` を作成し、  
ご自身の Cities: Skylines 2 のインストールパスに変更してください。

```xml
<Project>
  <PropertyGroup>
    <GamePath>C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II</GamePath>
  </PropertyGroup>
</Project>
```

### 2. ビルド

```bash
dotnet build
```

### 3. インストール

ビルド成果物を CS2 の Mod ディレクトリにコピーします。  
Mod は `Application.persistentDataPath` 配下を参照するため、以下のパスに配置してください：

```
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CS2_Translate_Mod\
├── CS2_Translate_Mod.dll
└── Translations/
    └── (翻訳JSONファイル)
```

> **注意**: `%LOCALAPPDATA%` (AppData\Local) ではなく `AppData\LocalLow` です。

---

## 翻訳JSONファイルの形式

```json
{
  "modId": "_extracted_mod_Anarchy",
  "modName": "Extracted from mod_Anarchy",
  "version": "2026-02-20",
  "entries": {
    "Options.SECTION[Anarchy.Anarchy.AnarchyMod]": {
      "original": "Anarchy",
      "translation": "アナーキー"
    },
    "Options.OPTION_DESCRIPTION[Anarchy.Anarchy.AnarchyMod.AnarchyToggle]": {
      "original": "Enables Anarchy mode which allows placing buildings without restrictions.",
      "translation": "建物の配置制限を無視するアナーキーモードを有効にします。"
    }
  }
}
```

| フィールド | 説明 |
|---|---|
| `modId` | Mod の識別子（メタデータ） |
| `modName` | Mod の名前（メタデータ） |
| `version` | 翻訳ファイルのバージョン |
| `entries` | 翻訳エントリの辞書 |
| `entries[key].original` | 原文（抽出元ロケールのテキスト。デフォルトは英語） |
| `entries[key].translation` | 訳文（注入先の言語）。空文字列 `""` は未翻訳として扱われスキップされる |

---

## 使い方

1. 翻訳したい Mod の翻訳 JSON ファイルを `Translations` フォルダに配置する
2. ゲームを起動する（または設定画面から「翻訳を再読み込み」ボタンを押す）
3. 翻訳が対象ロケール（デフォルト: 日本語）に自動適用される
4. ゲーム言語を切り替えた場合も、3秒のデバウンス後に自動的に再注入される

> **補足**: 現在のデフォルト注入先は `ja-JP`（日本語）です。他のロケールに注入する場合はソースコード内の `LocalizationInjector` を通じて任意のロケールIDを指定できます。

### ゲーム内設定

オプション画面の「Mod翻訳」セクションから以下を設定できます：

| セクション | 設定 | 説明 |
|---|---|---|
| 設定 | 翻訳を有効にする | 翻訳の読み込みを有効/無効化 |
| 設定 | デバッグログを有効にする | 詳細なログ出力（読み込み・注入・抽出の診断情報） |
| 設定 | 翻訳を再読み込み | 翻訳ファイルを再読み込みして即座に適用 |
| 抽出 | **翻訳キーを抽出** | **インストール済みModのローカライゼーションキーをMod別に抽出** |

---

## 翻訳キーの抽出（Mod別）

本Modでは、インストール済みのModからローカライゼーションキーを **Mod ごとに自動抽出** できます。

### 多段階抽出の仕組み

抽出はMod特定の精度を最大化するために3つのフェーズで実行されます：

| フェーズ | 方式 | 説明 |
|---|---|---|
| **Phase 1** | ソースベース（推奨） | `LocalizationManager` の `m_UserSources` からソースごとに抽出。ソースのアセンブリ情報でMod別にグループ化。最も高精度。 |
| **Phase 1.5** | 補完抽出 | `activeDictionary` から Phase 1 で漏れたModキーを補完。MemorySource経由で登録されたエントリなどを救済。 |
| **Phase 2** | 辞書ベース（フォールバック） | Phase 1 + 1.5 が0件の場合のみ実行。`activeDictionary` の全エントリをキーパターン解析でMod別にグルーピング。 |

- ゲーム本体（バニラ）のキーは **アセンブリ情報** および **キープレフィックスパターン**（約70カテゴリ）で自動除外
- 本Mod自身が注入した翻訳ソース（MemorySource）は `IsOurSource()` で判定しスキップ

### 抽出方法

1. ゲームを起動し、翻訳したいModが有効化されている状態で  
   オプション画面 →「Mod翻訳」→「抽出」セクション →「翻訳キーを抽出」ボタンを押す
2. `Translations` フォルダに Mod ごとの JSON ファイルが生成される

### 抽出ファイルの例

```
Translations/
├── _extracted_mod_Anarchy.json         # Anarchy Mod の翻訳テンプレート
├── _extracted_mod_FindIt.json          # Find It Mod の翻訳テンプレート
├── _extracted_mod_ExtraLib.json        # Extra Lib Mod の翻訳テンプレート
└── _extracted_mod__ungrouped.json      # Mod識別が不明なキー
```

### 既訳のマージ

既に翻訳済みのファイルが存在する場合、抽出しても **既存の翻訳は上書きされません**。  
新しいキーが追加され、既訳はそのまま保持されます。  
既存ファイルの読み込みに失敗した場合は `.bak` バックアップが自動作成されます。

---

## 翻訳ファイルの作成

新しい Mod の翻訳ファイルを作成するには：

1. ゲーム内設定画面から「翻訳キーを抽出」ボタンを押す
2. `Translations` フォルダに Mod ごとの JSON ファイルが自動生成される
3. 生成された JSON ファイルの `"translation"` フィールドに訳文を入力する
4. ゲーム内設定画面から「翻訳を再読み込み」ボタンを押す（またはゲーム再起動）

手動で作成する場合は、ファイル名を `_modname.json` のようにMod名を含めることを推奨します。

---

## トラブルシューティング

技術的な問題の詳細については [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) を参照してください。  
以下の問題について記録があります：

- Assembly パス取得の問題（CS2 のアセットシステム特有）
- 設定UIのローカライズキー不一致
- `onActiveDictionaryChanged` コールバックスパム
- リフレクションによる `LocalizationManager` 内部構造へのアクセス

---

## ライセンス

[LICENSE](LICENSE) を参照してください。