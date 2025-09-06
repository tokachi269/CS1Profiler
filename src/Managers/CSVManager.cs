using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CS1Profiler.Profiling;
using CS1Profiler.Core;

namespace CS1Profiler.Managers
{
    /// <summary>
    /// CSV出力管理クラス（軽量版：元のシンプル方式）
    /// </summary>
    public class CSVManager
    {
        private string _mainCsvFilePath;
        private bool _csvInitialized = false;
        private readonly List<string> _csvBuffer = new List<string>();
        private readonly object _bufferLock = new object(); // 最小限のlock追加

        public void Initialize()
        {
            if (_csvInitialized) return;

            try
            {
                string gameDirectory = Application.dataPath;
                if (gameDirectory.EndsWith("_Data"))
                {
                    gameDirectory = Directory.GetParent(gameDirectory).FullName;
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _mainCsvFilePath = Path.Combine(gameDirectory, string.Format("CS1Profiler_{0}.csv", timestamp));

                // シンプルヘッダー
                lock (_bufferLock)
                {
                    _csvBuffer.Add("MethodName,TotalDuration(ms),CallCount,AvgDuration(ms),MaxDuration(ms)");
                }
                
                _csvInitialized = true;
                Debug.Log(string.Format($"{Constants.LOG_PREFIX} CSV initialized: {0}", _mainCsvFilePath));
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format($"{Constants.LOG_PREFIX} CSV initialization failed: {0}", e.Message));
                _csvInitialized = false;
            }
        }

        // 超軽量：エンキューのみ（MPSCに移譲）
        public void LogMethodExecution(string methodName, double durationMs, int callCount)
        {
            // MPSC Logger に移譲（レガシー互換性）
            // 新システムでは LightweightPerformanceHooks が直接 MPSCLogger を使用
            Debug.LogWarning($"{Constants.LOG_PREFIX} LogMethodExecution is deprecated. Use MPSC system instead.");
        }

        // 一括出力：配列から直接CSV作成（最小限フォーマット）
        public void ExportAll()
        {
            Debug.LogWarning($"{Constants.LOG_PREFIX} ExportAll is deprecated. Use MPSC system instead.");
        }

        // 生データを書き込み（平均計算なし）
        public void ExportAllRawData()
        {
            Debug.LogWarning($"{Constants.LOG_PREFIX} ExportAllRawData is deprecated. Use MPSC system instead.");
        }

        public string GetCsvPath()
        {
            return _mainCsvFilePath ?? "CS1Profiler_Unknown.csv";
        }

        public string GetCsvFilePath()
        {
            return _mainCsvFilePath ?? "CSV not initialized";
        }

        public void Cleanup()
        {
            try
            {
                // 新システムでは何もしない（MPSCが処理）
                _csvInitialized = false;
                Debug.Log($"{Constants.LOG_PREFIX} CSV cleanup completed (legacy mode)");
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format($"{Constants.LOG_PREFIX} CSV cleanup error: {0}", e.Message));
            }
        }

        // 旧互換性メソッド（軽量化：何もしない）
        public void LogCurrentStats()
        {
            // 何もしない（軽量化のため）
        }

        public void ExportToCSV()
        {
            ExportAll();
        }

        public void QueuePerformanceData(object data)
        {
            // 何もしない（軽量化のため）
        }

        public void QueueCsvWrite(string category, string eventType, double durationMs, int count, double memoryMB, int rank, string description = "")
        {
            // 何もしない（軽量化のため）
        }
    }
}
