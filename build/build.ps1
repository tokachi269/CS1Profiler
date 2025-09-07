Write-Host "=== CS1 Profiler Build Script ===" -ForegroundColor Green
Write-Host "Building mod..." -ForegroundColor Yellow

$projectFile = "CS1Profiler.csproj"
dotnet build $projectFile --configuration Release --verbosity minimal

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build successful!" -ForegroundColor Green
    
    $sourceDll = "output\CS1Profiler.dll"
    if (Test-Path $sourceDll) {
        $dllInfo = Get-Item $sourceDll
        Write-Host "📦 DLL: $($dllInfo.Length) bytes" -ForegroundColor Cyan
        
        $outputDir = "$env:LOCALAPPDATA\Colossal Order\Cities_Skylines\Addons\Mods\CS1Profiler"
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        Copy-Item $sourceDll $outputDir -Force
        
        # 翻訳ファイルをコピー
        $translationsSourceDir = "src\Translations"
        $translationsTargetDir = "$outputDir\Translations"
        if (Test-Path $translationsSourceDir) {
            New-Item -ItemType Directory -Path $translationsTargetDir -Force | Out-Null
            Copy-Item "$translationsSourceDir\*.xml" $translationsTargetDir -Force
            Write-Host "📝 Translation files copied!" -ForegroundColor Cyan
        }
        
        Write-Host "✅ DLL deployed to MOD folder!" -ForegroundColor Green
        Write-Host "🎮 Restart Cities: Skylines to test!" -ForegroundColor Yellow
    } else {
        Write-Host "❌ DLL not found" -ForegroundColor Red
    }
} else {
    Write-Host "❌ Build failed" -ForegroundColor Red
}
