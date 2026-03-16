# ERGenerator

SQLファイルからER図（Entity-Relationship Diagram）を自動生成するツールです。Mermaid.jsを使用してインタラクティブなHTMLファイルを出力します。

## 特徴

- **SQLファイル自動解析**: DDL（CREATE TABLE文）を解析してテーブル構造を抽出
- **サブシステム分割**: JSON定義により、複数のサブシステムごとにER図を生成
- **多重度表記**: `1:1`, `1:N`, `N:N` などの直感的な表記をMermaid形式に自動変換
- **インタラクティブ操作**: ズーム機能搭載のHTML出力（拡大・縮小・リセット）
- **ELKレイアウト**: Mermaid の ELK レイアウトで複雑なER図を見やすく配置
- **スタンドアロン実行**: 単一のExeファイルで動作

## 使用方法

### コマンド

```bash
ERGenerator.exe <SQLファイルディレクトリ> <JSONファイルパス>
```

### 引数

| 引数 | 説明 | 例 |
|------|------|-----|
| `<SQLファイルディレクトリ>` | SQLファイルが格納されているディレクトリパス | `C:\Database\SQL` |
| `<JSONファイルパス>` | サブシステム定義のJSONファイルパス | `C:\Database\relations.json` |

### 実行例

```bash
ERGenerator.exe C:\Database\SQL C:\Database\relations.json
```

## JSONファイル形式

### 基本構造

```json
{
  "SubSystemName1": {
    "tables": ["Table1", "Table2"],
    "relations": [
      { "from": "Table1", "to": "Table2", "type": "1:N", "label": "説明" }
    ]
  },
  "SubSystemName2": {
    "tables": ["Table3", "Table4"],
    "relations": [
      { "from": "Table3", "to": "Table4", "type": "N:N", "label": "関連" }
    ]
  }
}
```

### サンプル

```json
{
  "OrderSystem": {
    "tables": ["Orders", "OrderDetails", "Menus"],
    "relations": [
      { "from": "Orders", "to": "OrderDetails", "type": "1:N", "label": "含む" },
      { "from": "Menus", "to": "OrderDetails", "type": "1:N", "label": "参照" }
    ]
  },
  "UserSystem": {
    "tables": ["Users", "Orders"],
    "relations": [
      { "from": "Users", "to": "Orders", "type": "1:N", "label": "注文履歴" }
    ]
  }
}
```

### 多重度タイプ

| タイプ | 説明 | Mermaid記号 |
|--------|------|-------------|
| `1:1` | 1対1 | `\|\|--\|\|` |
| `1:N` | 1対多 | `\|\|--o{` |
| `N:1` | 多対1 | `}o--\|\|` |
| `N:N` | 多対多 | `}o--o{` |
| `0..1:1` | 0または1対1 | `\|o--\|\|` |
| `1:0..1` | 1対0または1 | `\|\|--o\|` |
| `0..1:0..1` | 0または1対0または1 | `\|o--\|o` |
| `1:0..*` | 1対0以上 | `\|\|--o{` |
| `0..*:1` | 0以上対1 | `}o--\|\|` |

## 出力

各サブシステムごとに `{SubSystemName}.html` ファイルが生成されます。

### 生成されるHTMLの機能

- **ズームコントロール**: 右上のボタンで拡大・縮小・リセット
- **Ctrl+スクロール**: マウスホイールでズーム操作
- **フルスクリーン対応**: ER図が画面全体に表示
- **Mermaid.js**: インタラクティブなダイアグラム表示
- **ELKレイアウト**: 複雑なリレーションでも自動配置を改善

## ビルド方法

### 前提条件

- .NET 10.0 SDK 以降
- Windows環境

### ビルド手順

```bash
# リリースビルド（単一Exeファイル生成）
.\build_exe.bat
```

または

```bash
dotnet publish -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true
```

### 出力先

```
bin\Release\net10.0\win-x64\publish\ERGenerator.exe
```

## SQLファイル要件

### サポートされるDDL

- CREATE TABLE文
- PRIMARY KEY制約（インラインまたは独立定義）
- FOREIGN KEY制約

### サンプルSQL

```sql
CREATE TABLE Users (
    user_id INT PRIMARY KEY,
    username VARCHAR(50)
);

CREATE TABLE Orders (
    order_id INT PRIMARY KEY,
    user_id INT,
    FOREIGN KEY (user_id) REFERENCES Users(user_id)
);

CREATE TABLE OrderDetails (
    order_id INT,
    menu_id INT,
    quantity INT,
    PRIMARY KEY (order_id, menu_id),
    FOREIGN KEY (order_id) REFERENCES Orders(order_id)
);
```

## トラブルシューティング

### エラー: 引数が足りません

コマンドライン引数を2つ指定してください。

```bash
ERGenerator.exe <SQLディレクトリ> <JSONファイル>
```

### エラー: SQLファイルが見つかりません

指定したディレクトリに `.sql` ファイルが存在するか確認してください。

### エラー: JSONファイルが見つかりません

JSON ファイルのパスが正しいか確認してください。

### 警告: テーブルが表示されない

- JSON の `tables` 配列にテーブル名が含まれているか確認
- テーブル名の大文字小文字が一致しているか確認

## ライセンス
MIT License

## 依存ライブラリ

- [Mermaid.js](https://mermaid.js.org/) - ダイアグラム描画ライブラリ（CDN経由）
