using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CS1Profiler.Profiling;

namespace CS1Profiler.Managers
{
    /// <summary>
    /// CSV出力管理クラス（要件対応版）
    /// </summary>
    public class CSVManager
    {
        private bool _csvInitialized = false;
        private string _gameDirectory;
        private string _csvFilePath; // 後方互換性のため追加
        private readonly List<string> _csvBuffer = new List<string>(); // 後方互換性のため追加

        public void Initialize()
        {
            if (_csvInitialized) return;

            try
            {
                Debug.Log("[CS1Profiler] Starting CSV initialization...");

                _gameDirectory = Application.dataPath;
                if (_gameDirectory.EndsWith("_Data"))
                {
                    _gameDirectory = Directory.GetParent(_gameDirectory).FullName;
                }

                // 後方互換性のため既存形式のCSVパスも設定
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _csvFilePath = Path.Combine(_gameDirectory, $"CS1Profiler_{timestamp}.csv");
                
                _csvBuffer.Add("DateTime,FrameCount,Category,EventType,Duration(ms),Count,MemoryMB,Rank,Description");
                
                _csvInitialized = true;
                Debug.Log($"[CS1Profiler] CSV initialized. Output directory: {_gameDirectory}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] CSV initialization failed: {e.Message}");
                _csvInitialized = false;
            }
        }

        // 要件対応: TopNをCSVに出力
        public void ExportTopN(int topN)
        {
            if (!_csvInitialized)
            {
                Debug.LogError("[CS1Profiler] CSV not initialized");
                return;
            }

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"CS1Profiler_Top{topN}_{timestamp}.csv";
                string filePath = Path.Combine(_gameDirectory, fileName);

                string csvContent = MethodProfiler.GetCSVReportTopN(topN);
                File.WriteAllText(filePath, csvContent);

                Debug.Log($"[CS1Profiler] Top{topN} CSV exported: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] ExportTopN failed: {e.Message}");
            }
        }

        // 要件対応: 全メソッドをCSVに出力
        public void ExportAll()
        {
            if (!_csvInitialized)
            {
                Debug.LogError("[CS1Profiler] CSV not initialized");
                return;
            }

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"CS1Profiler_All_{timestamp}.csv";
                string filePath = Path.Combine(_gameDirectory, fileName);

                string csvContent = MethodProfiler.GetCSVReportAll();
                File.WriteAllText(filePath, csvContent);

                Debug.Log($"[CS1Profiler] All methods CSV exported: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] ExportAll failed: {e.Message}");
            }
        }

        // 軽量版: 生データを直接出力（平均計算なし）
        public void ExportAllRawData()
        {
            if (!_csvInitialized)
            {
                Debug.LogError("[CS1Profiler] CSV not initialized");
                return;
            }

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"CS1Profiler_RawData_{timestamp}.csv";
                string filePath = Path.Combine(_gameDirectory, fileName);

                string csvContent = MethodProfiler.GetCSVReportAll(); // 既に軽量化済み
                File.WriteAllText(filePath, csvContent);

                Debug.Log($"[CS1Profiler] Raw data CSV exported: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] ExportAllRawData failed: {e.Message}");
            }
        }

        // 既存のQueueCsvWriteメソッドは後方互換性のため保持
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

                string csvLine = $"{timestamp},{frame},{category},{eventType},{durationMs:F12},{count},{memoryMB:F2},{rank},{description}";
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
            if (!_csvInitialized || string.IsNullOrEmpty(_csvFilePath) || _csvBuffer.Count == 0) return;

            try
            {
                using (var writer = new StreamWriter(_csvFilePath, true))
                {
                    foreach (string line in _csvBuffer)
                    {
                        writer.WriteLine(line);
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
            return _csvFilePath ?? "CSV not initialized";
        }

        public string GetCsvPath()
        {
            return _csvFilePath ?? "CS1Profiler_Unknown.csv";
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

        public void LogCurrentStats()
        {
            try
            {
                Debug.Log("[CS1Profiler] === Current Performance Stats ===");
                var stats = CS1Profiler.Profiling.PerformanceProfiler.GetTopMethods(10);
                foreach (var stat in stats)
                {
                    Debug.Log($"[CS1Profiler] {stat.MethodName}: {stat.AverageMilliseconds:F3}ms avg ({stat.CallCount} calls)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] LogCurrentStats error: {e.Message}");
            }
        }

        public void ExportToCSV()
        {
            try
            {
                var stats = CS1Profiler.Profiling.PerformanceProfiler.GetTopMethods(100);
                foreach (var stat in stats)
                {
                    QueueCsvWrite("Performance", "Method", stat.AverageMilliseconds, stat.CallCount, 0, 0, stat.MethodName);
                }
                FlushCsvBuffer();
                Debug.Log($"[CS1Profiler] Exported {stats.Count} method stats to CSV");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] ExportToCSV error: {e.Message}");
            }
        }

        // 旧PerformanceProfiler互換性メソッド
        public void QueuePerformanceData(object data)
        {
            Debug.Log("[CS1Profiler] QueuePerformanceData called but disabled for lightweight profiling");
        }
    }
}
