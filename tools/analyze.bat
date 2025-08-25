@echo off
echo CS1Profiler Analysis Tool
echo ========================

REM 最新のCSVファイルを自動検出
for /f "delims=" %%i in ('dir /b /od "*.csv" 2^>nul ^| findstr /e ".csv" ^| tail -n 1') do set LATEST_CSV=%%i

if "%LATEST_CSV%"=="" (
    echo CSVファイルが見つかりません。
    echo このバッチファイルをCS1ProfilerのCSVがあるフォルダに置いて実行してください。
    pause
    exit /b 1
)

echo 最新のCSVファイル: %LATEST_CSV%
echo.

REM Python環境チェック
python --version >nul 2>&1
if errorlevel 1 (
    echo Pythonがインストールされていません。
    echo Python 3.8以上をインストールしてください。
    pause
    exit /b 1
)

REM 依存関係チェック（pandasがあるかどうか）
python -c "import pandas" >nul 2>&1
if errorlevel 1 (
    echo 依存関係をインストール中...
    pip install pandas numpy matplotlib seaborn
)

REM 解析実行
echo 解析を開始します...
python "%~dp0cs1_profiler_analyzer.py" "%LATEST_CSV%" -o "analysis_%date:~0,4%%date:~5,2%%date:~8,2%"

echo.
echo 解析完了! 結果フォルダを確認してください。
pause
