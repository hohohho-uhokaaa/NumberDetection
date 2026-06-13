using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf.Rendering;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Resources;
using System.Text.Json;

namespace NumberDetection
{
    #region データモデルクラス

    /// <summary>
    /// 画像属性情報を保持するクラス
    /// 処理結果のJSON出力に使用される
    /// </summary>
    class ImageAttributes
    {
        /// <summary>
        /// 元のファイル名
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// 出力ファイル名
        /// </summary>
        public string OutputFileName { get; set; } = string.Empty;

        /// <summary>
        /// 画像の幅（ピクセル）
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 画像の高さ（ピクセル）
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 用紙サイズ（A3/A4）
        /// </summary>
        public string PaperSize { get; set; } = string.Empty;

        /// <summary>
        /// 向き（縦/横）
        /// </summary>
        public string Orientation { get; set; } = string.Empty;

        /// <summary>
        /// 水平DPI
        /// </summary>
        public float HorizontalDpi { get; set; }

        /// <summary>
        /// 垂直DPI
        /// </summary>
        public float VerticalDpi { get; set; }

        /// <summary>
        /// 検出された数字の数
        /// </summary>
        public int DetectedNumberCount { get; set; }

        /// <summary>
        /// 検出された数字の詳細情報リスト
        /// </summary>
        public List<DetectedNumber> DetectedNumbers { get; set; } = new List<DetectedNumber>();

        /// <summary>
        /// 処理日時
        /// </summary>
        public DateTime ProcessedAt { get; set; }
    }

    /// <summary>
    /// 検出された数字の情報を保持するクラス
    /// </summary>
    class DetectedNumber
    {
        /// <summary>
        /// 検出番号
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// 左上X座標（ピクセル）
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 左上Y座標（ピクセル）
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 横サイズ（幅、ピクセル）
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 縦サイズ（高さ、ピクセル）
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 面積（ピクセル²）
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// アスペクト比（高さ/幅）
        /// </summary>
        public double AspectRatio { get; set; }
    }

    #endregion

    /// <summary>
    /// 画像処理プログラム
    /// 入力された画像またはPDFから数字を検出し、結果を出力する
    /// 機能:
    /// - PDFの複数ページ処理（最大50ページ）
    /// - 画像サイズ検証（A3/A4）
    /// - DPI検証（200-600 DPI）
    /// - 数字検出と枠描画
    /// - JSON形式での属性情報出力
    /// </summary>
    class Program
    {
        /// <summary>
        /// リソースマネージャー
        /// </summary>
        private static readonly ResourceManager _resources = new ResourceManager("NumberDetection.Resources", typeof(Program).Assembly);

        #region 定数定義

        /// <summary>
        /// 許可される最小DPI
        /// </summary>
        private const int MinDpi = 200;

        /// <summary>
        /// 許可される最大DPI
        /// </summary>
        private const int MaxDpi = 600;

        /// <summary>
        /// PDF変換時のDPI
        /// </summary>
        private const int PdfConversionDpi = 300;

        /// <summary>
        /// PDFの最大ページ数
        /// </summary>
        private const int MaxPdfPages = 50;

        /// <summary>
        /// 出力ディレクトリ名
        /// </summary>
        private const string OutputDirectory = "output";

        /// <summary>
        /// 輪郭の最小サイズ（ピクセル）
        /// 小さすぎるノイズを除外するため
        /// </summary>
        private const int MinContourSize = 10;

        /// <summary>
        /// 輪郭の最小面積（ピクセル²）
        /// </summary>
        private const int MinContourArea = 100;

        /// <summary>
        /// 輪郭の最大面積（ピクセル²）
        /// 大きすぎる領域を除外するため
        /// </summary>
        private const int MaxContourArea = 50000;

        /// <summary>
        /// 最小アスペクト比（高さ/幅）
        /// 数字は通常縦長であることを前提
        /// </summary>
        private const double MinAspectRatio = 0.5;

        /// <summary>
        /// 最大アスペクト比（高さ/幅）
        /// </summary>
        private const double MaxAspectRatio = 5.0;

        #endregion

        #region メイン処理

        /// <summary>
        /// メインエントリーポイント
        /// コマンドライン引数で指定されたファイルを処理する
        /// PDFの場合はすべてのページ（最大50ページ）を処理し、画像の場合は単一ファイルを処理する
        /// </summary>
        /// <param name="args">コマンドライン引数（入力ファイルパス）</param>
        static void Main(string[] args)
        {
            // 引数チェック
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            string inputPath = args[0];

            try
            {
                // 出力ディレクトリの作成
                CreateOutputDirectory();

                // 入力ファイルの処理（PDF変換、形式チェック）
                // PDFの場合は複数ページを画像に変換、画像の場合はそのままリスト化
                List<string>? imagePaths = ProcessInputFile(inputPath);
                if (imagePaths == null || imagePaths.Count == 0)
                {
                    return;
                }

                Console.WriteLine($"\n全{imagePaths.Count}枚の画像を処理します...\n");

                // 各画像を順次処理
                for (int i = 0; i < imagePaths.Count; i++)
                {
                    string imagePath = imagePaths[i];
                    Console.WriteLine($"========================================");
                    Console.WriteLine($"画像 {i + 1}/{imagePaths.Count}: {Path.GetFileName(imagePath)}");
                    Console.WriteLine($"========================================");

                    // 画像の読み込み
                    using (Mat image = LoadImage(imagePath))
                    {
                        if (image == null || image.IsEmpty)
                        {
                            Console.WriteLine($"画像を読み込めませんでした: {imagePath}");
                            continue; // エラー時は次の画像へ継続
                        }

                        // 画像サイズの検証（A3/A4チェック）
                        var (size, orientation) = DetectPaperSize(image);
                        if (size == null)
                        {
                            continue; // サイズ不正時は次の画像へ継続
                        }

                        // 画像DPIの検証（200-600 DPI範囲チェック）
                        float horizontalDpi, verticalDpi;
                        if (!ValidateImageDpi(imagePath, out horizontalDpi, out verticalDpi))
                        {
                            continue; // DPI不正時は次の画像へ継続
                        }

                        // 画像処理（グレースケール、二値化、輪郭検出、数字検出）
                        // imageIndexは出力ファイル名の識別子として使用
                        ProcessImage(image, i + 1, imagePath, size, orientation, horizontalDpi, verticalDpi);
                    }
                }

                Console.WriteLine("\n========================================");
                Console.WriteLine($"全{imagePaths.Count}枚の画像の処理が完了しました");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラーが発生しました: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// 使用方法を表示する
        /// </summary>
        static void PrintUsage()
        {
            string? usage = _resources.GetString("Usage_Instructions");
            string? formats = _resources.GetString("Supported_Formats");
            Console.WriteLine(usage ?? "使用方法: NumberDetection <入力ファイルパス>");
            Console.WriteLine(formats ?? "対応形式: jpg, jpeg, png, pdf");
        }

        /// <summary>
        /// 出力ディレクトリを作成する
        /// 既に存在する場合は何もしない
        /// </summary>
        static void CreateOutputDirectory()
        {
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
                string? message = _resources.GetString("OutputDirectory_Created");
                Console.WriteLine(message != null ? string.Format(message, OutputDirectory) : $"出力ディレクトリを作成しました: {OutputDirectory}");
            }
        }

        #endregion

        #region 入力ファイル処理

        /// <summary>
        /// 入力ファイルを処理する
        /// PDFの場合はすべてのページ（最大50ページ）を300 DPIで画像に変換し、
        /// 画像の場合はそのままパスをリストにして返す
        /// </summary>
        /// <param name="inputPath">入力ファイルパス</param>
        /// <returns>処理後の画像パスリスト（失敗時はnull）</returns>
        static List<string>? ProcessInputFile(string inputPath)
        {
            // 入力ファイルの存在チェック
            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"エラー: ファイルが見つかりません: {inputPath}");
                return null;
            }

            string extension = Path.GetExtension(inputPath).ToLower();

            // PDFの場合は画像に変換（すべてのページ）
            if (extension == ".pdf")
            {
                Console.WriteLine("PDFファイルを検出しました。300 DPIで画像に変換します...");
                List<string>? imagePaths = ConvertPdfToImage(inputPath);
                if (imagePaths == null || imagePaths.Count == 0)
                {
                    Console.WriteLine("PDFの変換に失敗しました");
                    return null;
                }
                return imagePaths;
            }

            // 対応していない形式の場合
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                Console.WriteLine($"対応していないファイル形式です: {extension}");
                Console.WriteLine("対応形式: jpg, jpeg, png, pdf");
                return null;
            }

            // 画像の場合はリストにして返す
            return new List<string> { inputPath };
        }

        /// <summary>
        /// 画像を読み込む
        /// Emgu.CVを使用してカラー画像として読み込む
        /// </summary>
        /// <param name="imagePath">画像ファイルパス</param>
        /// <returns>読み込んだ画像</returns>
        static Mat LoadImage(string imagePath)
        {
            return CvInvoke.Imread(imagePath, ImreadModes.Color);
        }

        #endregion

        #region 画像検証

        /// <summary>
        /// 画像サイズを検証する（A3/A4チェック）
        /// 300 DPI基準のA3またはA4サイズであることを確認する
        /// </summary>
        /// <param name="image">検証する画像</param>
        /// <returns>サイズが有効な場合はtrue、それ以外はfalse</returns>
        static bool ValidateImageSize(Mat image)
        {
            Console.WriteLine($"画像サイズ: {image.Width} x {image.Height} ピクセル");

            // 用紙サイズと向きを検出
            var (size, orientation) = DetectPaperSize(image);
            if (size == null)
            {
                PrintAllowedSizes();
                return false;
            }

            Console.WriteLine($"検出されたサイズ: {size} {orientation}");
            return true;
        }

        /// <summary>
        /// 用紙サイズと向きを検出する
        /// 300 DPI基準のA3/A4サイズ（縦/横）と照合する
        /// 許容誤差±5ピクセルで判定する
        /// </summary>
        /// <param name="image">検出対象の画像</param>
        /// <returns>(用紙サイズ, 向き) 検出できない場合は(null, null)</returns>
        static (string? size, string? orientation) DetectPaperSize(Mat image)
            {
            // 300 DPI基準の用紙サイズ定義
            var paperSizes = new Dictionary<string, (int portraitWidth, int portraitHeight)>
            {
                { "A3", (3508, 4961) },
                { "A4", (2480, 3508) }
            };

            // 許容誤差（ピクセル）
            const int tolerance = 5;

            // 各用紙サイズと向きをチェック
            foreach (var (paperSize, dimensions) in paperSizes)
            {
                // 縦向きチェック（許容誤差範囲内）
                if (Math.Abs(image.Width - dimensions.portraitWidth) <= tolerance &&
                    Math.Abs(image.Height - dimensions.portraitHeight) <= tolerance)
                    return (paperSize, "縦");
                // 横向きチェック（許容誤差範囲内）
                if (Math.Abs(image.Width - dimensions.portraitHeight) <= tolerance &&
                    Math.Abs(image.Height - dimensions.portraitWidth) <= tolerance)
                    return (paperSize, "横");
            }

            return (null, null);
        }

        /// <summary>
        /// 許可される用紙サイズを表示する
        /// </summary>
        static void PrintAllowedSizes()
        {
            Console.WriteLine("警告: 画像サイズがA3またはA4の標準サイズではありません");
            Console.WriteLine("許可されるサイズ:");
            Console.WriteLine("  A3縦: 3508 x 4961 ピクセル");
            Console.WriteLine("  A3横: 4961 x 3508 ピクセル");
            Console.WriteLine("  A4縦: 2480 x 3508 ピクセル");
            Console.WriteLine("  A4横: 3508 x 2480 ピクセル");
        }

        /// <summary>
        /// 画像のDPIを検証する
        /// 200-600 DPIの範囲内であることを確認する
        /// </summary>
        /// <param name="imagePath">画像ファイルパス</param>
        /// <param name="horizontalDpi">水平DPI（出力パラメータ）</param>
        /// <param name="verticalDpi">垂直DPI（出力パラメータ）</param>
        /// <returns>DPIが有効範囲内の場合はtrue、それ以外はfalse</returns>
        static bool ValidateImageDpi(string imagePath, out float horizontalDpi, out float verticalDpi)
        {
            horizontalDpi = 0;
            verticalDpi = 0;

            using (Bitmap bitmap = new Bitmap(imagePath))
            {
                horizontalDpi = bitmap.HorizontalResolution;
                verticalDpi = bitmap.VerticalResolution;
                Console.WriteLine($"画像解像度: {horizontalDpi:F2} x {verticalDpi:F2} DPI");

                // DPI範囲チェック（200-600 DPI）
                if (horizontalDpi < MinDpi || horizontalDpi > MaxDpi || verticalDpi < MinDpi || verticalDpi > MaxDpi)
                {
                    Console.WriteLine($"取り扱えないDPIです: {horizontalDpi:F2} x {verticalDpi:F2} DPI");
                    Console.WriteLine($"許可されるDPI範囲: {MinDpi} - {MaxDpi} DPI");
                    return false;
                }

                Console.WriteLine($"DPIチェック: OK（範囲内: {MinDpi} - {MaxDpi} DPI）");
                return true;
            }
        }

        #endregion

        #region 画像処理

        /// <summary>
        /// 画像処理を実行する
        /// 処理フロー: グレースケール変換 → 二値化 → ノイズ除去 → 輪郭検出 → 数字検出 → 結果保存
        /// </summary>
        /// <param name="image">処理対象の画像</param>
        /// <param name="imageIndex">画像のインデックス（複数画像処理時のファイル名用）</param>
        /// <param name="originalImagePath">元の画像パス</param>
        /// <param name="paperSize">用紙サイズ</param>
        /// <param name="orientation">向き</param>
        /// <param name="horizontalDpi">水平DPI</param>
        /// <param name="verticalDpi">垂直DPI</param>
        static void ProcessImage(Mat image, int imageIndex, string originalImagePath, string paperSize, string orientation, float horizontalDpi, float verticalDpi)
        {
            // グレースケール変換
            using (Mat gray = ConvertToGrayscale(image))
            // 二値化
            using (Mat binary = ThresholdImage(gray))
            // ノイズ除去用カーネル
            using (Mat kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1)))
            {
                // ノイズ除去
                RemoveNoise(binary, kernel);
                
                // 輪郭検出
                using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
                {
                    FindContours(binary, contours);
                    // 数字の検出と枠描画、検出結果を取得
                    List<DetectedNumber> detectedNumbers = DetectAndDrawNumbers(image, contours);
                    
                    // 画像属性情報を作成
                    ImageAttributes attributes = new ImageAttributes
                    {
                        OriginalFileName = Path.GetFileName(originalImagePath),
                        OutputFileName = string.Empty, // SaveResultImageで設定
                        Width = image.Width,
                        Height = image.Height,
                        PaperSize = paperSize,
                        Orientation = orientation,
                        HorizontalDpi = horizontalDpi,
                        VerticalDpi = verticalDpi,
                        DetectedNumberCount = detectedNumbers.Count,
                        DetectedNumbers = detectedNumbers,
                        ProcessedAt = DateTime.Now
                    };
                    
                    // 結果画像の保存（JSONも保存）
                    SaveResultImage(image, imageIndex, attributes);
                }
            }
        }

        /// <summary>
        /// 画像をグレースケールに変換する
        /// BGRからグレースケールへの色空間変換を行う
        /// </summary>
        /// <param name="image">変換対象の画像</param>
        /// <returns>グレースケール画像</returns>
        static Mat ConvertToGrayscale(Mat image)
        {
            Console.WriteLine("処理中: グレースケール変換...");
            Mat gray = new Mat();
            CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);
            Console.WriteLine("完了: グレースケール変換");
            return gray;
        }

        /// <summary>
        /// 画像を二値化する（大津の手法を使用）
        /// 自動的に閾値を決定して二値化を行う
        /// </summary>
        /// <param name="gray">グレースケール画像</param>
        /// <returns>二値化画像</returns>
        static Mat ThresholdImage(Mat gray)
        {
            Console.WriteLine("処理中: 二値化...");
            Mat binary = new Mat();
            CvInvoke.Threshold(gray, binary, 0, 255, ThresholdType.Otsu | ThresholdType.BinaryInv);
            Console.WriteLine("完了: 二値化");
            return binary;
        }

        /// <summary>
        /// ノイズを除去する（モルフォロジー処理）
        /// クロージング処理で小さな穴や隙間を埋める
        /// </summary>
        /// <param name="binary">二値化画像</param>
        /// <param name="kernel">モルフォロジー処理用カーネル</param>
        static void RemoveNoise(Mat binary, Mat kernel)
        {
            Console.WriteLine("処理中: ノイズ除去...");
            CvInvoke.MorphologyEx(binary, binary, MorphOp.Close, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());
            Console.WriteLine("完了: ノイズ除去");
        }

        /// <summary>
        /// 輪郭を検出する
        /// 外輪郭のみを検出する
        /// </summary>
        /// <param name="binary">二値化画像</param>
        /// <param name="contours">検出された輪郭</param>
        static void FindContours(Mat binary, VectorOfVectorOfPoint contours)
        {
            Console.WriteLine("処理中: 輪郭検出...");
            CvInvoke.FindContours(binary, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
            Console.WriteLine("完了: 輪郭検出");
        }

        #endregion

        #region 数字検出

        /// <summary>
        /// 数字を検出して枠を描画する
        /// 有効な輪郭をフィルタリングし、画像に緑色の枠を描画する
        /// </summary>
        /// <param name="image">描画対象の画像</param>
        /// <param name="contours">検出された輪郭</param>
        /// <returns>検出された数字のリスト</returns>
        static List<DetectedNumber> DetectAndDrawNumbers(Mat image, VectorOfVectorOfPoint contours)
        {
            Console.WriteLine("\n検出された数字の枠情報:");
            Console.WriteLine("========================================");

            List<DetectedNumber> detectedNumbers = new List<DetectedNumber>();
            int count = 0;
            for (int i = 0; i < contours.Size; i++)
            {
                // 輪郭の境界矩形を取得
                Rectangle rect = CvInvoke.BoundingRectangle(contours[i]);

                // 有効な輪郭かチェック
                if (!IsValidContour(rect))
                    continue;

                count++;
                // 輪郭情報を表示
                PrintContourInfo(count, rect);
                // 枠を描画（緑色、太さ2）
                CvInvoke.Rectangle(image, rect, new MCvScalar(0, 255, 0), 2);
                
                // 検出結果をリストに追加
                double aspectRatio = (double)rect.Height / rect.Width;
                double area = rect.Width * rect.Height;
                detectedNumbers.Add(new DetectedNumber
                {
                    Number = count,
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height,
                    Area = area,
                    AspectRatio = aspectRatio
                });
            }

            Console.WriteLine($"========================================");
            Console.WriteLine($"合計検出数: {count} 個");
            
            return detectedNumbers;
        }

        /// <summary>
        /// 輪郭が有効かチェックする
        /// サイズ、アスペクト比、面積でフィルタリングを行う
        /// </summary>
        /// <param name="rect">輪郭の境界矩形</param>
        /// <returns>有効な輪郭の場合はtrue、それ以外はfalse</returns>
        static bool IsValidContour(Rectangle rect)
        {
            // サイズチェック（小さすぎるノイズを除外）
            if (rect.Width < MinContourSize || rect.Height < MinContourSize)
                return false;

            // アスペクト比チェック（数字は通常縦長であることを前提）
            double aspectRatio = (double)rect.Height / rect.Width;
            if (aspectRatio < MinAspectRatio || aspectRatio > MaxAspectRatio)
                return false;

            // 面積チェック（小さすぎる/大きすぎる領域を除外）
            double area = rect.Width * rect.Height;
            if (area < MinContourArea || area > MaxContourArea)
                return false;

            return true;
        }

        /// <summary>
        /// 輪郭情報を表示する
        /// </summary>
        /// <param name="count">検出番号</param>
        /// <param name="rect">輪郭の境界矩形</param>
        static void PrintContourInfo(int count, Rectangle rect)
        {
            double aspectRatio = (double)rect.Height / rect.Width;
            double area = rect.Width * rect.Height;

            Console.WriteLine($"数字 {count}:");
            Console.WriteLine($"  左上X座標: {rect.X} ピクセル");
            Console.WriteLine($"  左上Y座標: {rect.Y} ピクセル");
            Console.WriteLine($"  横サイズ（幅）: {rect.Width} ピクセル");
            Console.WriteLine($"  縦サイズ（高さ）: {rect.Height} ピクセル");
            Console.WriteLine($"  面積: {area} ピクセル²");
            Console.WriteLine($"  アスペクト比: {aspectRatio:F2}");
            Console.WriteLine();
        }

        #endregion

        #region 出力処理

        /// <summary>
        /// 結果画像を保存する
        /// 出力ディレクトリを使用し、ファイル名に日付時刻を追加
        /// 同じ名前のJSONファイルに画像属性を保存
        /// </summary>
        /// <param name="image">保存対象の画像</param>
        /// <param name="imageIndex">画像のインデックス（複数画像処理時のファイル名用）</param>
        /// <param name="attributes">画像属性情報</param>
        static void SaveResultImage(Mat image, int imageIndex, ImageAttributes attributes)
        {
            Console.WriteLine("処理中: 結果画像を保存...");
            
            // タイムスタンプを生成（yyyyMMdd_HHmmssfff形式、ミリ秒まで含む）
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            
            // 出力ファイル名を生成（元のファイル名 + タイムスタンプ）
            // 複数画像の場合はインデックスも追加
            string baseFileName = Path.GetFileNameWithoutExtension(attributes.OriginalFileName);
            string outputFileName = imageIndex == 1 
                ? $"{baseFileName}_{timestamp}.png" 
                : $"{baseFileName}_{timestamp}_{imageIndex}.png";
            
            string outputPath = Path.Combine(OutputDirectory, outputFileName);
            attributes.OutputFileName = outputFileName;
            
            // 画像を保存
            CvInvoke.Imwrite(outputPath, image);
            Console.WriteLine($"完了: 枠を描画した画像を保存しました: {outputPath}");
            
            // JSONファイルを保存
            bool jsonSaved = SaveJsonAttributes(attributes);
            if (!jsonSaved)
            {
                Console.WriteLine("警告: JSONファイルの保存に失敗しました");
            }
        }

        /// <summary>
        /// 画像属性情報をJSONファイルとして保存する
        /// 日本語対応、インデント付きで保存する
        /// </summary>
        /// <param name="attributes">画像属性情報</param>
        /// <returns>保存成功の場合はtrue、失敗の場合はfalse</returns>
        static bool SaveJsonAttributes(ImageAttributes attributes)
        {
            try
            {
                // JSONファイルパスを生成（画像ファイルと同じ名前、拡張子は.json）
                string jsonPath = Path.ChangeExtension(Path.Combine(OutputDirectory, attributes.OutputFileName), ".json");
                
                // JSONオプション設定（日本語対応、インデント付き）
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                // JSONファイルに保存
                string jsonString = JsonSerializer.Serialize(attributes, options);
                File.WriteAllText(jsonPath, jsonString);
                
                Console.WriteLine($"完了: 画像属性を保存しました: {jsonPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON保存エラー: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region PDF処理

        /// <summary>
        /// PDFを300 DPIで画像に変換する（すべてのページ）
        /// 最大50ページまで処理可能
        /// 変換された画像はoutputディレクトリに保存される
        /// </summary>
        /// <param name="pdfPath">PDFファイルパス</param>
        /// <returns>変換後の画像パスリスト（失敗時はnull）</returns>
        static List<string>? ConvertPdfToImage(string pdfPath)
        {
            try
            {
                // PDFドキュメントを開く
                using (PdfDocument document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import))
                {
                    // ページチェック
                    if (document.PageCount == 0)
                    {
                        Console.WriteLine("PDFにページがありません");
                        return null;
                    }

                    // 最大ページ数チェック（50ページ制限）
                    if (document.PageCount > MaxPdfPages)
                    {
                        Console.WriteLine($"警告: PDFのページ数が{MaxPdfPages}ページを超えています（{document.PageCount}ページ）");
                        Console.WriteLine($"最初の{MaxPdfPages}ページのみ処理します");
                    }

                    int pagesToProcess = Math.Min(document.PageCount, MaxPdfPages);
                    Console.WriteLine($"PDFの{pagesToProcess}ページを処理します...");

                    List<string> imagePaths = new List<string>();

                    // PDFレンダラーの設定（300 DPI）
                    PdfRenderer renderer = new PdfRenderer(document);
                    renderer.DpiX = PdfConversionDpi;
                    renderer.DpiY = PdfConversionDpi;

                    // 各ページをレンダリング
                    for (int pageIndex = 0; pageIndex < pagesToProcess; pageIndex++)
                    {
                        Console.WriteLine($"ページ {pageIndex + 1}/{pagesToProcess} を変換中...");

                        int width = renderer.GetPageWidth(pageIndex);
                        int height = renderer.GetPageHeight(pageIndex);

                        using (Bitmap bitmap = new Bitmap(width, height))
                        {
                            bitmap.SetResolution(PdfConversionDpi, PdfConversionDpi);

                            using (Graphics graphics = Graphics.FromImage(bitmap))
                            {
                                graphics.Clear(Color.White);
                                renderer.RenderPage(graphics, pageIndex);
                            }

                            // PNGとして保存（ページ番号を含むファイル名）
                            // 出力ディレクトリを使用
                            // 例: input.pdf → output/input_page1.png, output/input_page2.png, ...
                            string outputFileName = $"{Path.GetFileNameWithoutExtension(pdfPath)}_page{pageIndex + 1}.png";
                            string outputPath = Path.Combine(OutputDirectory, outputFileName);
                            bitmap.Save(outputPath, ImageFormat.Png);
                            imagePaths.Add(outputPath);
                        }
                    }

                    Console.WriteLine($"PDF変換完了: {pagesToProcess}ページ");
                    return imagePaths;
                }
            }
            catch (Exception ex)
            {
                // エラー時に作成された一時画像をクリーンアップ
                if (imagePaths != null)
                {
                    foreach (var path in imagePaths)
                    {
                        try
                        {
                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }
                        }
                        catch
                        {
                            // 削除失敗は無視
                        }
                    }
                }
                Console.WriteLine($"PDF変換エラー: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
