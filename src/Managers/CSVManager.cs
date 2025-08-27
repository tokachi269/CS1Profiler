using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CS1Profiler.Profiling;

namespace CS1Profiler.Managers
{
    /// <summary>
    /// CSV出力管理クラス（LoadingExtensionBase ライフサイクル対応）
    /// </summary>
    public class CSVManager
    {
        // ゲーム状態別のファイルパス
        private string _menuCsvFilePath;
        private string _loadingCsvFilePath;
        private string _inGameCsvFilePath;
        private bool _csvInitialized = false;
        
        // 状態別のCSVバッファ
        private readonly List<string> _menuCsvBuffer = new List<string>();
        private readonly List<string> _loadingCsvBuffer = new List<string>();
        private readonly List<string> _inGameCsvBuffer = new List<string>();
        
        // 現在の状態とレコーディング状態
        private string _currentState = "MainMenu";
        private bool _recordingEnabled = true; // 全体的な記録制御フラグ

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

                // 状態別のCSVファイルを作成
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _menuCsvFilePath = Path.Combine(gameDirectory, $"CS1Profiler_Menu_{timestamp}.csv");
                _loadingCsvFilePath = Path.Combine(gameDirectory, $"CS1Profiler_Loading_{timestamp}.csv");
                _inGameCsvFilePath = Path.Combine(gameDirectory, $"CS1Profiler_InGame_{timestamp}.csv");

                // 各ファイルのヘッダーを設定
                string header = "DateTime,FrameCount,Category,EventType,Duration(ms),Count,MemoryMB,Rank,Description";
                _menuCsvBuffer.Add(header);
                _loadingCsvBuffer.Add(header);
                _inGameCsvBuffer.Add(header);
                
                _csvInitialized = true;
                Debug.Log($"[CS1Profiler] CSV initialized with state-based files:");
                Debug.Log($"  Menu: {_menuCsvFilePath}");
                Debug.Log($"  Loading: {_loadingCsvFilePath}");
                Debug.Log($"  InGame: {_inGameCsvFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] CSV initialization failed: {e.Message}");
                _csvInitialized = false;
            }
        }

        /// <summary>
        /// ゲーム状態変更（LoadingExtensionBase から呼び出される）
        /// </summary>
        public void SwitchGameState(string newState)
        {
            try
            {
                if (!_csvInitialized) Initialize();
                
                string previousState = _currentState;
                _currentState = newState;
                
                // 前の状態のバッファをフラッシュ
                FlushCurrentBuffer(previousState);
                
                LogStateTransition($"State changed to {newState}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] Error handling state change: {e.Message}");
            }
        }
        
        /// <summary>
        /// 状態遷移をログに記録
        /// </summary>
        private void LogStateTransition(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                int frame = Time.frameCount;
                double memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                
                string csvLine = $"{timestamp},{frame},System,StateChange,0,0,{memoryMB:F2},0,{message}";
                
                // 現在の状態に対応するバッファに追加
                GetCurrentBuffer().Add(csvLine);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] Error logging state transition: {e.Message}");
            }
        }
        
        /// <summary>
        /// 記録の有効/無効を設定（ProfilerManagerから呼び出される）
        /// </summary>
        public void SetRecordingEnabled(bool enabled)
        {
            _recordingEnabled = enabled;
            Debug.Log($"[CS1Profiler] CSV Recording set to: {(enabled ? "ENABLED" : "DISABLED")}");
            
            if (!enabled)
            {
                // 無効時は現在のバッファをフラッシュして停止メッセージを記録
                string stopMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{Time.frameCount},System,RecordingStopped,0,0,0,0,RECORDING_DISABLED_BY_USER";
                GetCurrentBuffer().Add(stopMessage);
                FlushCurrentBuffer(_currentState);
            }
            else
            {
                // 有効時は開始メッセージを記録
                string startMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{Time.frameCount},System,RecordingStarted,0,0,0,0,RECORDING_ENABLED_BY_USER";
                GetCurrentBuffer().Add(startMessage);
            }
        }
        
        /// <summary>
        /// 記録が有効かどうかを取得
        /// </summary>
        public bool IsRecordingEnabled()
        {
            return _recordingEnabled;
        }

        
        /// <summary>
        /// 現在の状態に対応するCSVバッファを取得
        /// </summary>
        private List<string> GetCurrentBuffer()
        {
            switch (_currentState)
            {
                case "MainMenu":
                    return _menuCsvBuffer;
                case "Loading":
                    return _loadingCsvBuffer;
                case "InGame":
                case "InGameDelayed":
                    return _inGameCsvBuffer;
                default:
                    return _menuCsvBuffer; // デフォルト
            }
        }
        
        /// <summary>
        /// 指定した状態のバッファをファイルにフラッシュ
        /// </summary>
        private void FlushCurrentBuffer(string state)
        {
            try
            {
                List<string> buffer;
                string filePath;
                
                switch (state)
                {
                    case "MainMenu":
                        buffer = _menuCsvBuffer;
                        filePath = _menuCsvFilePath;
                        break;
                    case "Loading":
                        buffer = _loadingCsvBuffer;
                        filePath = _loadingCsvFilePath;
                        break;
                    case "InGame":
                    case "InGameDelayed":
                        buffer = _inGameCsvBuffer;
                        filePath = _inGameCsvFilePath;
                        break;
                    default:
                        return;
                }
                
                if (buffer.Count <= 1 || string.IsNullOrEmpty(filePath)) return; // ヘッダーのみの場合はスキップ
                
                FlushBufferToFile(buffer, filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] Error flushing buffer for state {state}: {e.Message}");
            }
        }
        
        /// <summary>
        /// バッファをファイルに書き込み
        /// </summary>
        private void FlushBufferToFile(List<string> buffer, string filePath)
        {
            if (buffer.Count == 0 || string.IsNullOrEmpty(filePath)) return;

            try
            {
                // バッファのコピーを作成して、Collection modified エラーを回避
                List<string> bufferCopy;
                lock (buffer)
                {
                    bufferCopy = new List<string>(buffer);
                    buffer.Clear();
                }

                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    foreach (string line in bufferCopy)
                    {
                        writer.WriteLine(line);
                    }
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] CSV flush error: {e.Message}");
            }
        }

        // メソッド実行を記録（リアルタイムでファイルに書き込み）
        public void LogMethodExecution(string methodName, double durationMs, int callCount)
        {
            try
            {
                if (!_csvInitialized) Initialize();
                if (!_csvInitialized) return;

                // 記録が無効な場合は何もしない
                if (!_recordingEnabled) return;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                int frame = Time.frameCount;
                double memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

                string csvLine = $"{timestamp},{frame},Performance,MethodExecution,{durationMs:F3},{callCount},{memoryMB:F2},0,{methodName}";
                
                var buffer = GetCurrentBuffer();
                lock (buffer)
                {
                    buffer.Add(csvLine);
                    
                    // バッファが大きくなりすぎた場合は即座にファイルに書き込み
                    if (buffer.Count > 100)
                    {
                        FlushCurrentBuffer(_currentState);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] LogMethodExecution failed: {e.Message}");
            }
        }

        // TopNデータを現在の状態のファイルに書き込み
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
                
                // コメント行は削除 - CSVとして不正なため
                
                int rank = 1;
                foreach (var kvp in sortedStats)
                {
                    var data = kvp.Value;
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    int frame = Time.frameCount;
                    double memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                    
                    string csvLine = $"{timestamp},{frame},Export,Top{n}Export,{data.AverageMs:F3},{data.CallCount},{memoryMB:F2},{rank},{kvp.Key}";
                    GetCurrentBuffer().Add(csvLine);
                    rank++;
                }
                
                FlushCurrentBuffer(_currentState);
                Debug.Log($"[CS1Profiler] Top{n} data logged to current state CSV file");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] ExportTopN failed: {e.Message}");
            }
        }

        // 全データを現在の状態のファイルに書き込み
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
                
                int rank = 1;
                foreach (var kvp in sortedStats)
                {
                    var data = kvp.Value;
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    int frame = Time.frameCount;
                    double memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                    
                    string csvLine = $"{timestamp},{frame},Export,AllExport,{data.AverageMs:F3},{data.CallCount},{memoryMB:F2},{rank},{kvp.Key}";
                    GetCurrentBuffer().Add(csvLine);
                    rank++;
                }
                
                FlushCurrentBuffer(_currentState);
                Debug.Log($"[CS1Profiler] All data logged to current state CSV file");
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
                GetCurrentBuffer().Add(csvLine);

                if (GetCurrentBuffer().Count > 50)
                {
                    FlushCurrentBuffer(_currentState);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CS1Profiler] CSV queue error: {e.Message}");
            }
        }

        /// <summary>
        /// 古いFlushCsvBufferメソッド（削除予定）
        /// </summary>
        [Obsolete("Use FlushCurrentBuffer instead")]
        private void FlushCsvBuffer()
        {
            FlushCurrentBuffer(_currentState);
        }

        public string GetCsvFilePath()
        {
            switch (_currentState)
            {
                case "MainMenu":
                    return _menuCsvFilePath ?? "Menu CSV not initialized";
                case "Loading":
                    return _loadingCsvFilePath ?? "Loading CSV not initialized";
                case "InGame":
                case "InGameDelayed":
                    return _inGameCsvFilePath ?? "InGame CSV not initialized";
                default:
                    return "CSV not initialized";
            }
        }

        public string GetCsvPath()
        {
            return GetCsvFilePath();
        }

        public void Cleanup()
        {
            try
            {
                // 各状態のバッファに終了メッセージを追加
                string endMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{Time.frameCount},System,ProfilingEnd,0,0,0,0,SESSION_END";
                
                if (_menuCsvBuffer.Count > 1)
                {
                    _menuCsvBuffer.Add(endMessage);
                    FlushBufferToFile(_menuCsvBuffer, _menuCsvFilePath);
                }
                if (_loadingCsvBuffer.Count > 1)
                {
                    _loadingCsvBuffer.Add(endMessage);
                    FlushBufferToFile(_loadingCsvBuffer, _loadingCsvFilePath);
                }
                if (_inGameCsvBuffer.Count > 1)
                {
                    _inGameCsvBuffer.Add(endMessage);
                    FlushBufferToFile(_inGameCsvBuffer, _inGameCsvFilePath);
                }
                
                _csvInitialized = false;
                Debug.Log("[CS1Profiler] CSV cleanup completed for all state files");
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
            ExportAll(); // 現在の状態のファイルに出力
        }

        public void QueuePerformanceData(object data)
        {
            Debug.Log("[CS1Profiler] QueuePerformanceData called but disabled for lightweight profiling");
        }
        
        /// <summary>
        /// 現在の記録状態を取得（デバッグ用）
        /// </summary>
        public string GetRecordingStatus()
        {
            return $"State: {_currentState}, Recording: {_recordingEnabled}, Files: Menu={_menuCsvFilePath != null}, Loading={_loadingCsvFilePath != null}, InGame={_inGameCsvFilePath != null}";
        }
    }
}
