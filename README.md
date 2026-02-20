# CS2_Translate_Mod

Cities: Skylines 2 の Mod を日本語化するための翻訳ローダー＆抽出 Mod です。  
翻訳 JSON ファイルを `Translations` フォルダに配置するだけで、他の Mod の UI を日本語化できます。  
また、インストール済み Mod のローカライゼーションキーを **Mod ごとに自動抽出** して翻訳テンプレートを生成できます。

---

## 機能

- **翻訳JSONの自動読み込み** — `Translations` フォルダ内の `.json` ファイルを自動検出・読み込み
- **日本語ロケールへの注入** — ゲームのローカライゼーションシステム (`ja-JP`) に翻訳を注入
- **複数Mod対応** — 複数の翻訳ファイルを同時に読み込み可能
- **Mod別キー抽出** — インストール済みModのローカライゼーションキーをMod単位で自動抽出
- **既訳マージ** — 抽出時、既存の翻訳ファイルがあれば既訳を保持してマージ
- **再読み込み機能** — ゲーム内設定画面から翻訳を再読み込み
- **デバッグログ** — 翻訳の読み込み・抽出状況を詳細に確認可能

---

## プロジェクト構成

```
CS2_Translate_Mod/
├── src/                                  # ソースコード
│   ├── Mod.cs                            # エントリポイント (IMod 実装)
│   ├── Setting.cs                        # ゲーム内設定
│   ├── Models/
│   │   └── TranslationData.cs            # 翻訳JSON データモデル
│   ├── Utils/
│   │   └── TranslationLoader.cs          # JSONファイル読み込み・パース
│   ├── Localization/
│   │   └── LocalizationInjector.cs       # ローカライゼーション注入
│   ├── Extraction/
│   │   └── TranslationExtractor.cs       # Mod別翻訳キー抽出
│   └── Systems/
│       ├── TranslationLoaderSystem.cs     # ECSシステム（読み込み制御）
│       └── TranslationExtractorSystem.cs  # ECSシステム（抽出制御）
├── Translations/                         # 翻訳JSONファイル配置先
│   └── _sample_Anarchy.json              # サンプル翻訳
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

ビルド成果物を CS2 の Mod ディレクトリにコピーします：

```
%LOCALAPPDATA%\Colossal Order\Cities Skylines II\Mods\CS2_Translate_Mod\
├── CS2_Translate_Mod.dll
└── Translations/
    └── (翻訳JSONファイル)
```

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
| `entries[key].original` | 原文（英語） |
| `entries[key].translation` | 訳文（日本語）。空文字列 `""` は未翻訳として扱われスキップされる |

---

## 使い方

1. 翻訳したい Mod の翻訳 JSON ファイルを `Translations` フォルダに配置する
2. ゲームを起動する（または設定画面から「翻訳を再読み込み」ボタンを押す）
3. ゲーム言語を日本語に設定していれば、翻訳が自動適用される

### ゲーム内設定

オプション画面の「Mod翻訳」セクションから以下を設定できます：

| 設定 | 説明 |
|---|---|
| 翻訳を有効にする | 翻訳の読み込みを有効/無効化 |
| デバッグログを有効にする | 詳細なログ出力 |
| 翻訳を再読み込み | 翻訳ファイルを再読み込みして適用 |
| **翻訳キーを抽出** | **インストール済みModのローカライゼーションキーをMod別に抽出** |

---

## 翻訳キーの抽出（Mod別）

本Modでは、インストール済みのModからローカライゼーションキーを **Mod ごとに自動抽出** できます。

### 抽出の仕組み

1. ゲームの `LocalizationManager` から全ローカライゼーションエントリを取得
2. エントリキーのパターンを解析し、Mod別にグループ化
3. 各Modごとに `_extracted_mod_{ModId}.json` を `Translations` フォルダに出力
4. ゲーム本体（バニラ）のキーは自動的に除外

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

---

## 翻訳ファイルの作成

新しい Mod の翻訳ファイルを作成するには：

1. ゲーム内設定画面から「翻訳キーを抽出」ボタンを押す
2. `Translations` フォルダに Mod ごとの JSON ファイルがMod別に自動生成される
3. 生成された JSON ファイルの `"translation"` フィールドに訳文を入力する
4. ゲーム内設定画面から「翻訳を再読み込み」ボタンを押す（またはゲーム再起動）

手動で作成する場合は、ファイル名を `_modname.json` のようにMod名を含めることを推奨します。

---

## ライセンス

[LICENSE](LICENSE) を参照してください。