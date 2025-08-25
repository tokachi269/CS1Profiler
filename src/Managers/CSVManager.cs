using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CS1Profiler.Profiling;

namespace CS1Profiler.Managers
{
    /// <summary>
    /// CSV出力管理クラス（以前のフォーマット対応版）
    /// </summary>
    public class CSVManager
    {
        private string _mainCsvFilePath;
        private bool _csvInitialized = false;
        private readonly List<string> _csvBuffer = new List<string>();

        public void Initialize()
        {
            if (_csvInitialized) return;

            try
            {
                Debug.Log("[CS1Profiler] Starting CSV initialization...");

                string gameDirectory = Application.dataPath;
                if (gameDirectory.EndsWith("_Data"))
                {
                    gameDirectory = Directory.GetParent(gameDirectory).FullName;
                }

                // 一つのファイルにすべてのデータを記録
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _mainCsvFilePath = Path.Combine(gameDirectory, $"CS1Profiler_{timestamp}.csv");

                // 以前のフォーマットでヘッダーを設定
                _csvBuffer.Add("DateTime,FrameCount,Category,EventType,Duration(ms),Count,MemoryMB,Rank,Description");
                
                _csvInitialized = true;
                Debug.Log($"[CS1Profiler] CSV initialized: {_mainCsvFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] CSV initialization failed: {e.Message}");
                _csvInitialized = false;
            }
        }

        // メソッド実行を記録（リアルタイムでファイルに書き込み）
        public void LogMethodExecution(string methodName, double durationMs, int callCount)
        {
            try
            {
                if (!_csvInitialized) Initialize();
                if (!_csvInitialized) return;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                int frame = Time.frameCount;
                double memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

                string csvLine = $"{timestamp},{frame},Performance,MethodExecution,{durationMs:F3},{callCount},{memoryMB:F2},0,{methodName}";
                
                _csvBuffer.Add(csvLine);
                
                // バッファが大きくなりすぎた場合は即座にファイルに書き込み
                if (_csvBuffer.Count > 100)
                {
                    FlushCsvBuffer();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] LogMethodExecution failed: {e.Message}");
            }
        }

        // TopNデータをメインファイルに書き込み
        public void ExportTopN(int n)
        {
            try
            {
                if (!_csvInitialized) Initialize();
                
                var stats = MethodProfiler.GetStats();
                if (stats == null || !stats.Any())
                {
                    Debug.LogWarning("[CS1Profiler] No data to export for TopN");
                    return;
                }

                var sortedStats = stats.OrderByDescending(kvp => kvp.Value.TotalMs).Take(n).ToList();
                
                _csvBuffer.Add($"# === TOP {n} METHODS EXPORT ===");
                
                int rank = 1;
                foreach (var kvp in sortedStats)
                {
                    var data = kvp.Value;
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    int frame = Time.frameCount;
                    double memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                    
                    string csvLine = $"{timestamp},{frame},Export,Top{n}Export,{data.AverageMs:F3},{data.CallCount},{memoryMB:F2},{rank},{kvp.Key}";
                    _csvBuffer.Add(csvLine);
                    rank++;
                }
                
                FlushCsvBuffer();
                Debug.Log($"[CS1Profiler] Top{n} data logged to main CSV file");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] ExportTopN failed: {e.Message}");
            }
        }

        // 全データをメインファイルに書き込み
        public void ExportAll()
        {
            try
            {
                if (!_csvInitialized) Initialize();
                
                var stats = MethodProfiler.GetStats();
                if (stats == null || !stats.Any())
                {
                    Debug.LogWarning("[CS1Profiler] No data to export for All");
                    return;
                }

                var sortedStats = stats.OrderByDescending(kvp => kvp.Value.TotalMs).ToList();
                
                _csvBuffer.Add("# === ALL METHODS EXPORT ===");
                
                int rank = 1;
                foreach (var kvp in sortedStats)
                {
                    var data = kvp.Value;
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    int frame = Time.frameCount;
                    double memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                    
                    string csvLine = $"{timestamp},{frame},Export,AllExport,{data.AverageMs:F3},{data.CallCount},{memoryMB:F2},{rank},{kvp.Key}";
                    _csvBuffer.Add(csvLine);
                    rank++;
                }
                
                FlushCsvBuffer();
                Debug.Log($"[CS1Profiler] All data logged to main CSV file");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] ExportAll failed: {e.Message}");
            }
        }

        // 生データを書き込み（平均計算なし）
        public void ExportAllRawData()
        {
            ExportAll(); // 同じファイルに書き込み
        }

        // 汎用CSVライン追加（以前の互換性維持）
        public void QueueCsvWrite(string category, string eventType, double durationMs, int count, double memoryMB, int rank, string description = "")
        {
            try
            {
                if (!_csvInitialized) Initialize();
                if (!_csvInitialized) return;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                int frame = Time.frameCount;

                if (memoryMB <= 0)
                {
                    memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                }

                string csvLine = $"{timestamp},{frame},{category},{eventType},{durationMs:F3},{count},{memoryMB:F2},{rank},{description}";
                _csvBuffer.Add(csvLine);

                if (_csvBuffer.Count > 50)
                {
                    FlushCsvBuffer();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] CSV queue error: {e.Message}");
            }
        }

        private void FlushCsvBuffer()
        {
            if (!_csvInitialized || string.IsNullOrEmpty(_mainCsvFilePath) || _csvBuffer.Count == 0) return;

            try
            {
                StreamWriter writer = null;
                try
                {
                    writer = new StreamWriter(_mainCsvFilePath, true);
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
                Debug.LogError($"[CS1Profiler] CSV flush error: {e.Message}");
            }
        }

        public string GetCsvFilePath()
        {
            return _mainCsvFilePath ?? "CSV not initialized";
        }

        public string GetCsvPath()
        {
            return _mainCsvFilePath ?? "CS1Profiler_Unknown.csv";
        }

        public void Cleanup()
        {
            try
            {
                if (_csvBuffer.Count > 0)
                {
                    _csvBuffer.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{Time.frameCount},System,ProfilingEnd,0,0,0,0,SESSION_END");
                    FlushCsvBuffer();
                }
                _csvInitialized = false;
                Debug.Log("[CS1Profiler] CSV cleanup completed");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] CSV cleanup error: {e.Message}");
            }
        }

        // 旧互換性メソッド
        public void LogCurrentStats()
        {
            QueueCsvWrite("System", "StatsSnapshot", 0, 0, 0, 0, "Performance stats logged");
        }

        public void ExportToCSV()
        {
            ExportAll(); // メインファイルに出力
        }

        public void QueuePerformanceData(object data)
        {
            Debug.Log("[CS1Profiler] QueuePerformanceData called but disabled for lightweight profiling");
        }
    }
}
