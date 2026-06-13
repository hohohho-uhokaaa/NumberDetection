# 画像数字検出プログラム

画像またはPDFから数字を検出し、その枠の位置とサイズをピクセル単位で表示するC#プログラムです。

## 機能

- **画像処理**: JPG、PNG、JPEG形式の画像を処理
- **PDF処理**: PDFファイルのすべてのページを300 DPIで画像に変換して処理（最大50ページ）
- **サイズ検証**: A3またはA4サイズ（縦/横）であることを確認
- **DPI検証**: 200-600 DPIの範囲内であることを確認
- **数字検出**: 各数字の枠の左上座標（X, Y）とサイズ（幅、高さ）をピクセルで表示
- **結果出力**: 
  - 枠付き画像を保存（outputディレクトリ）
  - JSON形式で画像属性情報を出力
- **ファイル名**: 元のファイル名 + 日付時刻（yyyyMMdd_HHmmss形式）

## 使用方法

### 1. プロジェクトのビルド

#### 通常ビルド

```bash
cd NumberDetection
dotnet restore
dotnet build
```

#### Self-contained ビルド（Windows x64）

```bash
cd NumberDetection
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained --trim
```

出力: `bin/Release/net10.0/win-x64/publish/`

#### Self-contained ビルド（Linux x64）

```bash
cd NumberDetection
dotnet restore
dotnet publish -c Release -r linux-x64 --self-contained --trim
```

出力: `bin/Release/net10.0/linux-x64/publish/`

#### --trim オプションについて

`--trim` オプションを指定すると、未使用のアセンブリが削除され、実行ファイルのサイズが小さくなります。

**効果:**
- 実行ファイルサイズの削減（通常30-50%程度の削減）
- 配布ファイルの軽量化
- 起動速度の向上（場合による）

**注意事項:**
- リフレクションを使用しているコードがある場合、意図しないアセンブリが削除される可能性があります
- 実行時に動的にアセンブリをロードする機能を使用している場合は注意が必要です
- 本番環境での使用前に十分なテストを行うことを推奨します

### 2. プログラムの実行

#### 通常実行

```bash
dotnet run -- <入力ファイルパス>
```

#### Self-contained 実行ファイルでの実行

**Windows:**
```bash
cd bin/Release/net8.0/win-x64/publish
NumberDetection.exe <入力ファイルパス>
```

**Linux:**
```bash
cd bin/Release/net8.0/linux-x64/publish
./NumberDetection <入力ファイルパス>
```

例:
```bash
dotnet run -- input_image.png
dotnet run -- document.pdf
```

### 3. 入力ファイル

- 対応フォーマット: JPG, JPEG, PNG, PDF
- 画像サイズ: A3またはA4（300 DPI基準）
- DPI範囲: 200-600 DPI

## 出力

### 出力ディレクトリ構造

```
output/
├── input_20240614_070500.png      (処理後の画像)
├── input_20240614_070500.json     (画像属性情報)
├── input_page1.png                (PDF変換画像)
├── input_page2.png
└── ...
```

### 出力例

```
画像サイズ: 2480 x 3508 ピクセル
検出されたサイズ: A4 縦
画像解像度: 300.00 x 300.00 DPI
DPIチェック: OK（範囲内: 200 - 600 DPI）

処理中: グレースケール変換...
完了: グレースケール変換
処理中: 二値化...
完了: 二値化
処理中: ノイズ除去...
完了: ノイズ除去
処理中: 輪郭検出...
完了: 輪郭検出

検出された数字の枠情報:
========================================
数字 1:
  左上X座標: 150 ピクセル
  左上Y座標: 200 ピクセル
  横サイズ（幅）: 45 ピクセル
  縦サイズ（高さ）: 80 ピクセル
  面積: 3600 ピクセル²
  アスペクト比: 1.78

数字 2:
  左上X座標: 250 ピクセル
  左上Y座標: 200 ピクセル
  横サイズ（幅）: 42 ピクセル
  縦サイズ（高さ）: 78 ピクセル
  面積: 3276 ピクセル²
  アスペクト比: 1.86

========================================
合計検出数: 2 個

処理中: 結果画像を保存...
完了: 枠を描画した画像を保存しました: output/input_20240614_070500.png
完了: 画像属性を保存しました: output/input_20240614_070500.json
```

### JSON出力例

```json
{
  "OriginalFileName": "input.png",
  "OutputFileName": "input_20240614_070500.png",
  "Width": 2480,
  "Height": 3508,
  "PaperSize": "A4",
  "Orientation": "縦",
  "HorizontalDpi": 300.0,
  "VerticalDpi": 300.0,
  "DetectedNumberCount": 2,
  "DetectedNumbers": [
    {
      "Number": 1,
      "X": 150,
      "Y": 200,
      "Width": 45,
      "Height": 80,
      "Area": 3600.0,
      "AspectRatio": 1.78
    },
    {
      "Number": 2,
      "X": 250,
      "Y": 200,
      "Width": 42,
      "Height": 78,
      "Area": 3276.0,
      "AspectRatio": 1.86
    }
  ],
  "ProcessedAt": "2024-06-14T07:05:00"
}
```

## 依存パッケージ

- Emgu.CV 4.8.0.5324
- Emgu.CV.Bitmap 4.8.0.5324
- Emgu.CV.runtime.linux 4.8.0.5324
- Emgu.CV.runtime.windows 4.8.0.5324
- PdfSharp 6.1.1
- PdfSharp.Drawing 6.1.1
- PdfSharp.Pdf.Rendering 1.0.0
- System.Text.Json 8.0.0

## 検出アルゴリズム

1. グレースケール変換（BGR → グレースケール）
2. 大津の二値化法による二値化
3. ノイズ除去（モルフォロジー演算 - クロージング）
4. 輪郭検出（外輪郭のみ）
5. サイズ、アスペクト比、面積によるフィルタリング
6. 境界矩形の計算と出力

## 定数設定

`Program.cs`の以下の定数を調整することで検出パラメータを変更できます：

- `MinDpi = 200`: 許可される最小DPI
- `MaxDpi = 600`: 許可される最大DPI
- `PdfConversionDpi = 300`: PDF変換時のDPI
- `MaxPdfPages = 50`: PDFの最大ページ数
- `MinContourSize = 10`: 輪郭の最小サイズ（ピクセル）
- `MinContourArea = 100`: 輪郭の最小面積（ピクセル²）
- `MaxContourArea = 50000`: 輪郭の最大面積（ピクセル²）
- `MinAspectRatio = 0.5`: 最小アスペクト比（高さ/幅）
- `MaxAspectRatio = 5.0`: 最大アスペクト比（高さ/幅）

## 用紙サイズ（300 DPI基準）

- A3縦: 3508 x 4961 ピクセル
- A3横: 4961 x 3508 ピクセル
- A4縦: 2480 x 3508 ピクセル
- A4横: 3508 x 2480 ピクセル

---

## 補足

このプロジェクトはすべてを **devin next** で作成しました。

**作成所要時間:** 2時間強程度

**devin next と GitHub 連携の効果:**
- コーディングミスを激減させながら効率的な開発作業が可能
- コードのロジック検討に全集中してスムーズに作業続行が可能、目的とした成果やレベルを短時間で実現できる
- 高品質化と保守性向上が平易なプロンプトで実現できる
- リファクタリングやコメント追加などの作業の繰り返しもスムーズで苦にならない

このエコシステムを積極的に使うことをおすすめします。AIアシストとバージョン管理の統合により、開発プロセス全体が大幅に改善されます。

---

## Cascade によるコードレビュー内容

コードレビューで発見された問題点と修正内容（すべてプロジェクトに反映済み）:

### 高優先度項目

1. **ResourceManagerのnull参照リスク修正**
   - 問題: `_resources.GetString()` の戻り値がnullの場合、`Console.WriteLine()` や `string.Format()` に渡されると `NullReferenceException` が発生する可能性
   - 修正: nullチェックとフォールバック文字列を追加

2. **PDF変換エラー時のリソースリーク修正**
   - 問題: PDF変換中に例外が発生した場合、作成された一時画像ファイルがクリーンアップされずに残る
   - 修正: 例外発生時に作成済みの画像ファイルを削除する処理を追加

3. **JSON保存失敗の通知改善**
   - 問題: JSON保存失敗時に例外がキャッチされるが、ユーザーに通知されず処理が成功したように見える
   - 修正: `SaveJsonAttributes` メソッドの戻り値をboolに変更し、呼び出し元で警告メッセージを表示

4. **入力ファイル存在チェック追加**
   - 問題: 入力ファイルが存在しない場合、後続処理で不明瞭なエラーメッセージが表示される
   - 修正: `ProcessInputFile` メソッドの先頭でファイル存在チェックを追加し、明確なエラーメッセージを表示

### 低・中優先度項目

5. **未使用のhierarchy Mat削除**
   - 問題: `FindContours` メソッドで作成されたhierarchy Matが使用されていない
   - 修正: 未使用のhierarchyパラメータを削除し、メソッドシグネチャを簡素化

6. **タイムスタンプ衝突の修正**
   - 問題: 単一画像処理時、1秒以内に複数回実行するとファイル名が衝突する可能性
   - 修正: タイムスタンプ形式を `yyyyMMdd_HHmmss` から `yyyyMMdd_HHmmssfff` (ミリ秒まで含む) に変更

7. **用紙サイズ判定の許容誤差追加**
   - 問題: 用紙サイズ判定が厳密な一致のみで、DPIやスケーリングの微小な差異で拒否される
   - 修正: ±5ピクセルの許容誤差を持つ判定に変更し、実用的な柔軟性を確保

