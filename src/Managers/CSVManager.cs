using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CS1Profiler.Managers
{
    /// <summary>
    /// CSV出力管理クラス（軽量版）
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
                Debug.Log("[CS1Profiler] Starting CSV initialization...");
                
                string gameDirectory = Application.dataPath;
                if (gameDirectory.EndsWith("_Data"))
                {
                    gameDirectory = Directory.GetParent(gameDirectory).FullName;
                }
                
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _csvFilePath = Path.Combine(gameDirectory, $"CS1Profiler_{timestamp}.csv");
                
                _csvBuffer.Add("DateTime,FrameCount,Category,EventType,Duration(ms),Count,MemoryMB,Rank,Description");
                
                _csvInitialized = true;
                Debug.Log($"[CS1Profiler] CSV initialized: {_csvFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] CSV initialization failed: {e.Message}");
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
