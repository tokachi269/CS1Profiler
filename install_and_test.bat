@echo off
echo ======================================
echo Cities: Skylines 1 起動性能解析ツール
echo ======================================
echo.

REM ビルドの確認
if not exist "output\CS1Profiler.dll" (
    echo [エラー] CS1Profiler.dll が見つかりません。
    echo 最初に dotnet build --configuration Release を実行してください。
    pause
    exit /b 1
)

echo [1] プロジェクトをビルド
dotnet build --configuration Release
if errorlevel 1 (
    echo [エラー] ビルドに失敗しました。
    pause
    exit /b 1
)

echo.
echo [2] DLLファイルの確認
dir output\CS1Profiler.dll

echo.
echo [3] Cities: Skylines Local Mods フォルダの確認
set MODS_PATH=%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods

if not exist "%MODS_PATH%" (
    echo [警告] Cities: Skylines Mods フォルダが見つかりません:
    echo %MODS_PATH%
    echo.
    echo Cities: Skylines がインストールされていることを確認してください。
) else (
    echo Mods フォルダ: %MODS_PATH%
    
    echo.
    echo [4] CS1Profiler フォルダの作成（存在しない場合）
    if not exist "%MODS_PATH%\CS1Profiler" (
        mkdir "%MODS_PATH%\CS1Profiler"
        echo CS1Profiler フォルダを作成しました。
    )
    
    echo.
    echo [5] DLLファイルのコピー
    copy "output\CS1Profiler.dll" "%MODS_PATH%\CS1Profiler\"
    if errorlevel 1 (
        echo [エラー] DLLファイルのコピーに失敗しました。
        pause
        exit /b 1
    ) else (
        echo DLLファイルを正常にコピーしました。
    )
)

echo.
echo ======================================
echo インストール完了！
echo ======================================
echo.
echo 次の手順:
echo 1. Cities: Skylines を起動してください
echo 2. Content Manager で CS1Profiler MOD を有効にしてください
echo 3. ゲームを起動して、起動時間を測定してください
echo 4. 生成された CSV ファイルを以下のコマンドで分析してください:
echo    python analyze_startup.py
echo.
echo 出力ファイル保存場所:
echo Documents\My Games\Cities_Skylines\
echo.
echo キーボードショートカット（ゲーム中）:
echo - P キー: プロファイリング ON/OFF
echo - L キー: ログ表示 ON/OFF  
echo - R キー: 現在の統計表示
echo.

pause
