# CS2_Translate_Mod トラブルシューティング記録

> **対象読者**: 今後このプロジェクトを保守・拡張する AI アシスタント  
> **作成日**: 2026-02-20  
> **対象環境**: Cities: Skylines 2 (Steam版) / .NET Framework 4.7.2 (net472) / CS2 Modding API

---

## 目次

1. [Assembly パス取得の問題](#1-assembly-パス取得の問題)
2. [設定UI のローカライズキー不一致](#2-設定ui-のローカライズキー不一致)
3. [onActiveDictionaryChanged コールバックスパム](#3-onactivedictionarychanged-コールバックスパム)
4. [抽出: リフレクションで LocalizationManager からエントリを取得できない](#4-抽出-リフレクションで-localizationmanager-からエントリを取得できない)
5. [抽出: m_UserSources が ValueTuple で IDictionarySource にキャストできない](#5-抽出-m_usersources-が-valuetuple-で-idictionarysource-にキャストできない)
6. [抽出: m_Dict が Dictionary\<string, Entry\> で string→string にキャストできない](#6-抽出-m_dict-が-dictionarystring-entry-で-stringstring-にキャストできない)
7. [CS2 LocalizationManager の内部構造まとめ](#7-cs2-localizationmanager-の内部構造まとめ)

---

## 1. Assembly パス取得の問題

### 症状

`OnLoad` 内で `typeof(Mod).Assembly.Location` が **空文字列** を返し、`Mod Path` が空 → `Translations` フォルダが不正な場所に作成される。

### 原因

CS2 は Mod の DLL を直接ファイルパスからロードするのではなく、**独自のアセットシステム経由でメモリにロード** する。このため .NET の `Assembly.Location` が空になる。

### 推測過程

1. 最初は `Assembly.Location` で済むと想定 → 実行時に空文字列
2. フォールバックとして `Assembly.CodeBase` を試行 → `file:///C:/Program Files (x86)/Steam/steamapps/common/data-0000024B806FA030` というゴミパスが返った
3. CS2 のアセットシステムが DLL をメモリバッファとして読み込んでいるため、どちらもファイルシステム上の正しいパスを返さないと判明

### 対処

`UnityEngine.Application.persistentDataPath` を使用してパスを構築:

```csharp
var persistentData = Application.persistentDataPath;
// → "C:/Users/{user}/AppData/LocalLow/Colossal Order/Cities Skylines II"
ModPath = Path.Combine(persistentData, "Mods", "CS2_Translate_Mod");
TranslationsPath = Path.Combine(ModPath, "Translations");
```

### 重要な注意点

- `Application.persistentDataPath` は Unity API なので `using UnityEngine;` が必要
- CS2 の Mod フォルダは `{persistentDataPath}/Mods/{ModName}/` に配置される仕様
- `Assembly.Location` / `Assembly.CodeBase` は CS2 Modding では **一切信頼できない**

---

## 2. 設定UI のローカライズキー不一致

### 症状

設定画面に生のキー文字列がそのまま表示される:
```
OPTIONS.GROUP[CS2_TRANSLATE_MOD.CS2_TRANSLATE_MOD.MOD.SETT...
OPTIONS.OPTION[CS2_Translate_Mod.CS2_Translate_Mod.Mod.Setting.EnableTranslation]
```

### 原因

CS2 が自動生成するローカライズキーの形式と、コード側で登録したキーの形式が不一致だった。

**CS2 が生成するキー形式**:
```
Options.SECTION[{AssemblyName}.{Namespace}.{ModClassName}]
Options.OPTION[{AssemblyName}.{Namespace}.{ModClassName}.{SettingClassName}.{PropertyName}]
```

**最初に登録していたキー形式** (間違い):
```
Options.SECTION[CS2_Translate_Mod]
Options.OPTION[CS2_Translate_Mod.Setting.EnableTranslation]
```

### 推測過程

1. 初期実装では `nameof(CS2_Translate_Mod)` でキーを構築 → アセンブリ名しか含まない
2. ゲーム画面のスクリーンショットから実際のキー文字列を読み取り、`{Assembly}.{Namespace}.{ClassName}` 形式であることを確認
3. CS2 は `IMod` 実装クラスの **完全修飾名** (`Assembly.GetName().Name + "." + Type.FullName`) をプレフィックスとして使用

### 対処

```csharp
var modType = typeof(Mod);
var modId = $"{modType.Assembly.GetName().Name}.{modType.FullName}";
// → "CS2_Translate_Mod.CS2_Translate_Mod.Mod"
```

このプレフィックスで全キーを構築。また、日本語 (ja-JP) だけでなく **英語 (en-US) フォールバック** も登録するようにした。

### 重要な注意点

- CS2 のキー形式は **大文字小文字を区別する**
- `nameof()` はクラス名のみ返すので、名前空間が同じだと足りない
- 設定UIのキーは `{Mod識別子}.{Settingクラス名}.{プロパティ名}` の階層構造
- **未確認**: この修正後も設定UIの日本語化が正しく機能するかは未検証。ゲーム側が MemorySource 注入のタイミングで辞書を参照するため、RegisterInOptionsUI() の後に注入する必要がある可能性あり

---

## 3. onActiveDictionaryChanged コールバックスパム

### 症状

ゲーム起動から数秒間に `Locale changed, requesting translation reload...` が **30回以上** 連続でログ出力される。

### 原因

`onActiveDictionaryChanged` は「アクティブ辞書が変更された」イベントだが、これは **ロケール切り替え以外にも発火する**:

- 他の Mod がローカライゼーションソースを追加した時
- 自分自身が `AddSource()` で翻訳を注入した時（注入 → イベント発火 → 再読み込み → 注入 → ...の再帰リスク）
- ゲーム本体が起動シーケンスで辞書を段階的に構築する時

### 推測過程

1. 最初は `SuppressLocaleCallback` フラグで自前の注入中だけ抑制 → 他 Mod の追加による発火は防げない
2. ログを見ると、Mod の `Loaded` 後に数秒間にわたって連続発火しており、これは自前の注入とは無関係
3. **デバウンス** (最後のイベントから一定時間待ってから1回だけ処理) が適切と判断

### 対処

2層の抑制:

1. **SuppressLocaleCallback フラグ** — 自前の `AddSource()` 呼び出し中は `true` にして再帰を完全ブロック
2. **デバウンス** — コールバックではタイムスタンプのみ記録し、`TranslationLoaderSystem.OnUpdate()` で最後のイベントから **3秒** 経過後に1回だけリロード

```csharp
// Mod.cs — コールバック
private void OnLocaleChanged()
{
    if (SuppressLocaleCallback) return;
    LastLocaleChangeTicks = DateTime.UtcNow.Ticks;
    LocaleChangePending = true;
}

// TranslationLoaderSystem.cs — OnUpdate
if (Mod.LocaleChangePending)
{
    var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Mod.LastLocaleChangeTicks);
    if (elapsed.TotalSeconds >= DebounceSeconds)
    {
        Mod.LocaleChangePending = false;
        LoadAndInjectTranslations();
    }
}
```

### 重要な注意点

- `onActiveDictionaryChanged` は UI スレッドで呼ばれる可能性があり、重い処理をコールバック内で行うべきではない
- デバウンス時間 3秒は起動シーケンスの長さに依存。Mod が多い環境ではもう少し長くする必要があるかもしれない
- `_reloadRequested`（手動リロード）はデバウンスなしで即時処理する設計にしている

---

## 4. 抽出: リフレクションで LocalizationManager からエントリを取得できない

### 症状

抽出ボタンを押すと `Retrieved 0 entries from sources (locale: en-US)` → 失敗。

### 原因

初期実装では以下のフィールド名を仮定していたが、すべて存在しなかった:
- `m_ActiveDictionary` → 実際は `<activeDictionary>k__BackingField`（C# auto-property のバッキングフィールド）
- `m_Sources` → 実際は `m_UserSources`
- `activeDictionary` プロパティは存在するが、`LocalizationDictionary` 型で `IDictionary<string, string>` を実装していない

### 推測過程

1. CS2 のソースコードは非公開なので、フィールド名は推測に基づいていた
2. リフレクションで **全フィールド/プロパティを列挙してログ出力** する診断コードを追加
3. ログから実際の内部構造が判明:

```
Field: <activeDictionary>k__BackingField (LocalizationDictionary)
Field: m_UserSources (List`1) = List`1 (Count=411)
Field: m_FallbackDictionary (LocalizationDictionary)
```

### 対処

4段階のアプローチを順次試行する設計に変更:
1. `activeDictionary` プロパティ → `LocalizationDictionary` 内部の `m_Dict` フィールド
2. `m_Dictionaries` マップ（存在しなかったがフォールバックとして残置）
3. `m_UserSources` からの `IDictionarySource.ReadEntries()`
4. 全フィールドの再帰的探索（最終フォールバック）

### 教訓

- CS2 の内部 API は非公開。**フィールド名を推測せず、まず全構造をログ出力して確認** すべき
- `LogManagerInternals()` のような診断メソッドを最初から組み込んでおくと効率的

---

## 5. 抽出: m_UserSources が ValueTuple で IDictionarySource にキャストできない

### 症状

`m_UserSources` が見つかってもエントリが0件。

### 原因

`m_UserSources` の実際の型:
```
List<ValueTuple<string, IDictionarySource>>
```

`ReadFromSourceList()` では `foreach` で各アイテムを `IDictionarySource` にキャストしていたが、アイテムは `ValueTuple<string, IDictionarySource>` であり、直接キャストは失敗する。

### 推測過程

1. ログに `m_UserSources` の型情報が出力された:
   ```
   System.Collections.Generic.List`1[[System.ValueTuple`2[[System.String,...],[Colossal.IDictionarySource,...]],...]]
   ```
2. `ValueTuple` の `Item2` が `IDictionarySource` であることが判明

### 対処

`ReadFromSourceList()` を修正し、各アイテムに対して:
1. 直接 `IDictionarySource` にキャストを試行
2. 失敗したら `Item2` フィールドをリフレクションで取得し、そこから `IDictionarySource` を取得

```csharp
if (item is IDictionarySource ds)
{
    dictSource = ds;
}
else
{
    var item2Field = item.GetType().GetField("Item2");
    if (item2Field != null)
    {
        var item2 = item2Field.GetValue(item);
        if (item2 is IDictionarySource ds2)
            dictSource = ds2;
    }
}
```

### 重要な注意点

- `ValueTuple` はフィールド名 `Item1`, `Item2` で固定（`Tuple` クラスの `.Item1` プロパティとは異なる）
- CS2 のバージョンアップでこの構造が変わる可能性があるため、どちらのパターンも試行する設計にしている

---

## 6. 抽出: m_Dict が Dictionary\<string, Entry\> で string→string にキャストできない

### 症状

`activeDictionary` 内の `m_Dict` (Count=29,529) が見つかるが、`IDictionary<string, string>` へのキャストが失敗し、エントリが取得できない。

### 原因

`m_Dict` の実際の型:
```
Dictionary<string, LocalizationDictionary.Entry>
```

`LocalizationDictionary.Entry` は独自の構造体/クラスであり、`string` ではない。そのため `IDictionary<string, string>` にはキャストできない。

### 推測過程

1. `LogManagerInternals()` の出力で `m_Dict` の存在と Count=29,529 を確認
2. 直接 `m_Dict` フィールドにアクセスするコードを追加し、型情報をログ出力:
   ```
   m_Dict actual type: Dictionary`2[[String,...],[LocalizationDictionary+Entry,...]]
   ```
3. `Entry` → `string` への変換が必要

### 対処

非ジェネリック `IDictionary` インタフェースで列挙し、`ToString()` でキーと値を取得:

```csharp
if (mDictObj is IDictionary nonGenericDict)
{
    foreach (DictionaryEntry de in nonGenericDict)
    {
        var k = de.Key?.ToString();
        var v = de.Value?.ToString();
        if (k != null) entries[k] = v ?? "";
    }
}
```

### 結果

`LocalizationDictionary.Entry` の `ToString()` が翻訳テキストを返すため、この方法で **29,529 エントリすべて** を正常に取得できた。

### 重要な注意点

- `Entry.ToString()` が常に正しいテキストを返す保証はない。将来的にはリフレクションで `Entry` の内部フィールド（`value` や `text` など）を直接読む方がより堅牢
- 非ジェネリック `IDictionary` はボクシングが発生するが、抽出は一度きりの操作なのでパフォーマンス問題にはならない

---

## 7. CS2 LocalizationManager の内部構造まとめ

2026-02-20 時点でのリフレクション調査結果:

### Colossal.Localization.LocalizationManager

| フィールド名 | 型 | 説明 |
|---|---|---|
| `<fallbackLocaleId>k__BackingField` | `string` | フォールバックロケール |
| `onActiveDictionaryChanged` | `Action` | アクティブ辞書変更イベント |
| `onSupportedLocalesChanged` | `Action` | サポートロケール変更イベント |
| `m_SuppressEvents` | `bool` | イベント抑制フラグ |
| `m_LocaleInfos` | `Dictionary<?, ?>` | ロケール情報 (Count=12) |
| `m_LocaleIdToLocalizedName` | `Dictionary<string, string>` | ロケールID→ローカライズ名 (Count=12) |
| `m_SystemLanguageToLocaleId` | `Dictionary<?, ?>` | システム言語→ロケールID (Count=12) |
| `<activeDictionary>k__BackingField` | `LocalizationDictionary` | **現在のアクティブ辞書** |
| `m_FallbackDictionary` | `LocalizationDictionary` | フォールバック辞書 |
| `m_UserSources` | `List<ValueTuple<string, IDictionarySource>>` | **ユーザーソースリスト (Count=411)** |

| プロパティ名 | 型 | 説明 |
|---|---|---|
| `fallbackLocaleId` | `string` | フォールバックロケールID |
| `activeDictionary` | `LocalizationDictionary` | アクティブ辞書 |
| `activeLocaleId` | `string` | 現在のロケールID |

### Colossal.Localization.LocalizationDictionary

| フィールド名 | 型 | 説明 |
|---|---|---|
| `log` | `ILog` | ロガー |
| `<localeID>k__BackingField` | `string` | ロケールID |
| `m_Dict` | `Dictionary<string, LocalizationDictionary.Entry>` | **全エントリ辞書 (Count=29,529)** |
| `<indexCounts>k__BackingField` | `Dictionary<?, ?>` | インデックスカウント (Count=274) |

### エントリ取得の最適パス

```
LocalizationManager
  └ activeDictionary (property)
      └ m_Dict (field, via reflection)
          └ foreach (DictionaryEntry) → Key.ToString(), Value.ToString()
```

---

## 全般的な教訓

1. **CS2 の Mod 環境では .NET 標準の前提が通用しない** — `Assembly.Location` が空、internal 型が多用されている
2. **リフレクションは推測でなく診断から始める** — まず全フィールド/プロパティをログ出力し、実際の型と構造を確認してから対処する
3. **イベントの発火頻度を仮定しない** — `onActiveDictionaryChanged` のように、想定外のタイミングで大量に発火するイベントがある
4. **非ジェネリック IDictionary を忘れない** — ジェネリック型パラメータが異なるとキャストが失敗するが、非ジェネリック版なら `ToString()` で読める
5. **ValueTuple のフィールドアクセスは `Item1`, `Item2`** — プロパティではなくフィールドでアクセスする
6. **CS2 のバージョンアップで内部構造が変わる可能性がある** — 複数のアプローチをフォールバック付きで実装しておく
