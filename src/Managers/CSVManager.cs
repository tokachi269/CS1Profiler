// Cities: Skylines (CS1) 用：CSV出力管理クラス

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CS1Profiler
{
    /// <summary>
    /// CSV出力管理クラス（インスタンス対応）
    /// </summary>
    public class CSVManager
    {
        private string _csvFilePath;
        private bool _csvInitialized = false;
        private readonly List<string> _csvBuffer = new List<string>();

        public void Initialize()
        {
            if (_csvInitialized) return;
            
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Starting CSV initialization for startup analysis...");
                
                // 出力ファイルパス: Cities Skylinesゲームインストールディレクトリ/CS1Profiler_startup_YYYYMMDD_HHMMSS.csv
                string gameDirectory = UnityEngine.Application.dataPath;
                
                // Unity Editorの場合はAssetsフォルダを含むが、実行時は実際のゲームディレクトリ
                // Cities: Skylinesの場合、dataPathは "CitiesSkylines_Data" フォルダを指すので、親ディレクトリを取得
                if (gameDirectory.EndsWith("_Data"))
                {
                    gameDirectory = Directory.GetParent(gameDirectory).FullName;
                }
                
                UnityEngine.Debug.Log("[CS1Profiler] Game directory detected: " + gameDirectory);
                
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _csvFilePath = gameDirectory + "\\CS1Profiler_startup_" + timestamp + ".csv";
                
                // CSVヘッダーをバッファに追加（起動解析用フォーマット）
                _csvBuffer.Add("DateTime,FrameCount,Category,EventType,Duration(ms),Count,MemoryMB,Rank,Description");
                
                _csvInitialized = true;
                UnityEngine.Debug.Log("[CS1Profiler] Startup analysis CSV initialized: " + _csvFilePath);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] CSV initialization failed: " + e.Message);
                UnityEngine.Debug.LogError("[CS1Profiler] Stack trace: " + e.StackTrace);
                _csvInitialized = false;
            }
        }

        public void QueueCsvWrite(string category, string eventType, double durationMs, int count, double memoryMB, int rank, string description = "")
        {
            try
            {
                if (!_csvInitialized) Initialize();
                if (!_csvInitialized) return;
                
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                int frame = UnityEngine.Time.frameCount;
                
                // メモリMBが指定されていない場合は現在のメモリ使用量を取得
                if (memoryMB <= 0)
                {
                    memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                }
                
                string csvLine = string.Format("{0},{1},{2},{3},{4:F3},{5},{6:F2},{7},{8}",
                    timestamp, frame, category, eventType, durationMs, count, memoryMB, rank, description);
                
                _csvBuffer.Add(csvLine);
                
                // バッファが大きくなりすぎた場合は即座にファイルに書き込み
                if (_csvBuffer.Count > 50)
                {
                    FlushCsvBuffer();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] CSV queue error: " + e.Message);
            }
        }

        private void FlushCsvBuffer()
        {
            if (!_csvInitialized || string.IsNullOrEmpty(_csvFilePath) || _csvBuffer.Count == 0) return;
            
            try
            {
                // .NET 3.5互換：usingを避けて明示的にDisposeを呼ぶ
                StreamWriter writer = null;
                try
                {
                    writer = new StreamWriter(_csvFilePath, true);
                    foreach (string line in _csvBuffer)
                    {
                        writer.WriteLine(line);
                    }
                    writer.Flush();
                }
                finally
                {
                    if (writer != null)
                    {
                        writer.Close();
                        writer = null;
                    }
                }
                _csvBuffer.Clear();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] CSV flush error: " + e.Message);
            }
        }

        public string GetCsvFilePath()
        {
            try
            {
                if (!_csvInitialized) Initialize();
                return _csvFilePath ?? "CSV not initialized";
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] GetCsvFilePath error: " + e.Message);
                return "CSV error: " + e.Message;
            }
        }

        public void Cleanup()
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] CSV cleanup starting...");
                
                // 残っているバッファがあれば安全に処理
                if (_csvBuffer != null && _csvBuffer.Count > 0)
                {
                    UnityEngine.Debug.Log("[CS1Profiler] Flushing remaining " + _csvBuffer.Count + " CSV entries...");
                    
                    // 最終セッション記録を追加
                    _csvBuffer.Add(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        UnityEngine.Time.frameCount,
                        "System", "ProfilingEnd", 0, 0, 0, 0, "SESSION_END"));
                    
                    // 最終書き込み実行
                    FlushCsvBuffer();
                }
                
                // 初期化フラグを最後にfalseにして新しい書き込みを防止
                _csvInitialized = false;
                
                UnityEngine.Debug.Log("[CS1Profiler] CSV output closed safely: " + (_csvFilePath ?? "null"));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] CSV cleanup error: " + e.Message);
                UnityEngine.Debug.LogError("[CS1Profiler] CSV cleanup stack trace: " + e.StackTrace);
            }
        }
    }
}
