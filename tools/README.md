# CS1Profiler Analysis Tool

Cities: Skylines 1のパフォーマンス解析ツール（Python版）

## 📋 機能

- **メソッド別統計**: 平均実行時間、スパイク検出、呼び出し回数分析
- **フレーム別分析**: フレームごとの負荷推移とボトルネック特定
- **カテゴリ別集計**: MOD/ゲーム機能別のパフォーマンス分類
- **問題検出**: 高負荷メソッド、スパイク多発、呼び出し異常を自動検出
- **可視化**: グラフとチャートでの視覚的分析
- **詳細レポート**: CSV形式での詳細統計とテキストレポート

## 🚀 セットアップ

### 1. Python環境の準備
```powershell
# Python 3.8以上が必要
python --version

# 仮想環境作成（推奨）
python -m venv cs1_profiler_env
cs1_profiler_env\Scripts\activate
```

### 2. 依存関係のインストール
```powershell
cd D:\GitHub\CS1Profiler\tools
pip install -r requirements.txt
```

## 📊 使用方法

### 基本的な使用方法
```powershell
# CS1ProfilerのCSVファイルを解析
python cs1_profiler_analyzer.py "C:\path\to\profiler_data.csv"
```

### オプション付き実行
```powershell
# 出力ディレクトリとスパイク検出感度を指定
python cs1_profiler_analyzer.py "profiler_data.csv" -o "my_analysis" -s 1.5
```

### 引数説明
- `csv_file`: CS1ProfilerのCSVファイルパス（必須）
- `-o, --output`: 出力ディレクトリ（デフォルト: analysis_output）
- `-s, --spike-multiplier`: スパイク検出の閾値倍率（デフォルト: 2.0）

## 📁 出力ファイル

解析結果は指定したディレクトリに保存されます：

### CSV統計ファイル
- `method_statistics.csv`: メソッド別詳細統計
- `frame_statistics.csv`: フレーム別統計
- `performance_issues.csv`: 検出された問題一覧

### 可視化グラフ（PNG）
- `top15_methods.png`: 高負荷メソッドTop15
- `category_impact.png`: カテゴリ別影響度（円グラフ）
- `frame_timeline.png`: フレーム別負荷推移
- `spike_analysis.png`: スパイク分析

### レポート
- `analysis_report.txt`: 総合解析レポート

## 🔍 解析指標

### メソッド統計
- **TotalCalls**: 総呼び出し回数
- **AvgDurationMs**: 平均実行時間
- **MaxDurationMs**: 最大実行時間
- **StdDevMs**: 標準偏差（安定性指標）
- **SpikeCount**: スパイク発生回数
- **AvgTotalPerFrameMs**: フレーム当たり平均影響時間
- **ImpactPercentage**: 全体に対する影響度パーセンテージ

### 問題検出
- **高負荷メソッド**: 全体の5%以上を占めるメソッド
- **スパイク多発**: 平均の2倍以上の実行時間が10回以上発生
- **呼び出し回数変動**: フレーム間で3倍以上の呼び出し回数差

## 💡 使用例

### Cities: Skylinesでデータ収集
1. CS1Profiler MODを有効化
2. ゲームプレイ（数分程度）
3. CSVファイルが自動生成される

### 解析実行
```powershell
# 基本解析
python cs1_profiler_analyzer.py "C:\Users\YourName\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\CS1Profiler\profiler_20250825_143022.csv"

# より敏感なスパイク検出
python cs1_profiler_analyzer.py "profiler_data.csv" -s 1.2 -o "detailed_analysis"
```

### 結果の見方
1. **コンソール出力**: 上位5メソッドと主要問題を即座に確認
2. **analysis_report.txt**: 詳細な文字レポート
3. **PNG画像**: 視覚的な傾向分析
4. **CSV統計**: Excelでさらに詳細分析

## 🔧 トラブルシューティング

### よくある問題

**Q: `ModuleNotFoundError: No module named 'pandas'`**
A: `pip install -r requirements.txt` で依存関係をインストール

**Q: 日本語が文字化けする**
A: システムにVS Code Powerユニコードフォントをインストール

**Q: グラフが表示されない**
A: matplotlib backendの問題。`pip install --upgrade matplotlib` を実行

**Q: CSVファイルの形式エラー**
A: CS1Profiler MODが正しく動作していることを確認。UTF-8形式で保存されているか確認

## 📈 高度な使用方法

### バッチ処理
複数のCSVファイルを一括処理する場合：

```powershell
# PowerShellスクリプト例
Get-ChildItem "*.csv" | ForEach-Object {
    python cs1_profiler_analyzer.py $_.Name -o "analysis_$($_.BaseName)"
}
```

### カスタム分析
スクリプトを改造して、特定のMODや機能に特化した解析も可能です。

## 🤝 サポート

問題や改善要望があれば、CS1Profilerのリポジトリにissueを作成してください。
