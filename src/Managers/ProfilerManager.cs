using System;
using System.Linq;

namespace CS1Profiler.Managers
{
    /// <summary>
    /// ProfilerManagerのインスタンス互換性クラス
    /// </summary>
    public class ProfilerManagerInstance
    {
        public CSVManager CsvManager => ProfilerManager.CsvManager;
        public bool IsProfilingEnabled() => ProfilerManager.IsProfilingEnabled;
        public void ToggleProfiling() => ProfilerManager.ToggleProfiling();
        public string GetCsvPath() => ProfilerManager.GetCsvPath();
        public void Initialize() => ProfilerManager.Initialize();
    }

    /// <summary>
    /// プロファイラーの全体管理（メソッド実行時間測定版）
    /// </summary>
    public static class ProfilerManager
    {
        private static PerformanceProfiler performanceProfiler;
        private static CSVManager csvManager;
        private static bool isProfilingEnabled = false;
        private static bool isInitialized = false;
        
        // 他のファイルとの互換性のための疑似インスタンス
        public static ProfilerManagerInstance Instance
        {
            get { return new ProfilerManagerInstance(); }
        }

        public static void Initialize()
        {
            try
            {
                if (isInitialized) return;

                // 基本的なパフォーマンス監視のみ初期化
                performanceProfiler = new PerformanceProfiler();
                csvManager = new CSVManager();

                // HarmonyでMethodProfilerを初期化
                if (CitiesHarmony.API.HarmonyHelper.IsHarmonyInstalled)
                {
                    var harmony = new HarmonyLib.Harmony("CS1Profiler.MethodProfiler");
                    CS1Profiler.Profiling.MethodProfiler.Initialize(harmony);
                    UnityEngine.Debug.Log("[CS1Profiler] MethodProfiler initialized with Harmony");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[CS1Profiler] Harmony is not installed, MethodProfiler disabled");
                }

                isInitialized = true;
                UnityEngine.Debug.Log("[CS1Profiler] ProfilerManager initialized");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ProfilerManager initialization failed: " + e.Message);
            }
        }

        public static void ToggleProfiling()
        {
            isProfilingEnabled = !isProfilingEnabled;
            
            if (isProfilingEnabled)
            {
                UnityEngine.Debug.Log("[CS1Profiler] Method profiling started");
            }
            else
            {
                UnityEngine.Debug.Log("[CS1Profiler] Method profiling stopped");
            }
        }

        public static bool IsProfilingEnabled
        {
            get { return isProfilingEnabled; }
        }

        public static string GetCsvPath()
        {
            return csvManager?.GetCsvFilePath() ?? "ProfileData.csv";
        }

        public static void OutputMethodStats()
        {
            try
            {
                if (csvManager != null)
                {
                    // MethodProfilerの統計データを取得して出力
                    UnityEngine.Debug.Log("[CS1Profiler] Method stats output requested");
                    
                    var stats = CS1Profiler.Profiling.MethodProfiler.GetStats();
                    UnityEngine.Debug.Log($"[CS1Profiler] Outputting stats for {stats.Count} methods");
                    
                    if (stats.Count > 0)
                    {
                        foreach (var kvp in stats)
                        {
                            var methodName = kvp.Key;
                            var data = kvp.Value;
                            
                            // CSVに基本的な統計情報を出力
                            csvManager.QueueCsvWrite(
                                category: "MethodProfiler",
                                eventType: "MethodStats",
                                durationMs: data.AverageMs,
                                count: data.CallCount,
                                memoryMB: 0,
                                rank: data.SpikeCount,
                                description: $"{methodName}|Max:{data.MaxMs:F1}ms|Spikes:{data.SpikeCount}|Total:{data.TotalMs:F1}ms"
                            );
                            
                            // スパイク詳細も出力
                            foreach (var spike in data.Spikes.OrderByDescending(s => s.Timestamp).Take(3))
                            {
                                csvManager.QueueCsvWrite(
                                    category: "SpikeDetail",
                                    eventType: "Spike",
                                    durationMs: spike.ExecutionTimeMs,
                                    count: 1,
                                    memoryMB: 0,
                                    rank: (int)spike.SpikeRatio,
                                    description: $"{methodName}|Ratio:{spike.SpikeRatio:F1}x|Avg:{spike.AverageAtTime:F2}ms|Caller:{spike.CallStackInfo}"
                                );
                            }
                        }
                    }
                    else
                    {
                        // データがない場合のテスト出力
                        csvManager.QueueCsvWrite(
                            category: "MethodProfiler",
                            eventType: "NoData",
                            durationMs: 0.0,
                            count: 0,
                            memoryMB: 0,
                            rank: 0,
                            description: "No method profiling data available"
                        );
                    }
                    
                    UnityEngine.Debug.Log("[CS1Profiler] Method stats output complete");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] OutputMethodStats error: " + e.Message);
            }
        }

        public static void OutputSpikeReport()
        {
            try
            {
                // TODO: MethodProfilerが正常にビルドされた後に有効化
                UnityEngine.Debug.Log("[CS1Profiler] Spike report functionality will be available after MethodProfiler build");
                
                /*
                var spikeReport = CS1Profiler.Profiling.MethodProfiler.GetSpikeReport();
                UnityEngine.Debug.Log("[CS1Profiler] " + spikeReport);
                
                var recentSpikes = CS1Profiler.Profiling.MethodProfiler.GetRecentSpikes(5);
                if (recentSpikes.Count > 0)
                {
                    UnityEngine.Debug.Log($"[CS1Profiler] Recent spikes (last 5 min): {recentSpikes.Count}");
                    foreach (var spike in recentSpikes.Take(5))
                    {
                        UnityEngine.Debug.Log($"[CS1Profiler] - {spike.Timestamp:HH:mm:ss}: {spike.ExecutionTimeMs:F2}ms ({spike.SpikeRatio:F1}x)");
                    }
                }
                */
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] OutputSpikeReport error: " + e.Message);
            }
        }

        public static string GetSpikeAnalysis()
        {
            try
            {
                // TODO: MethodProfilerが正常にビルドされた後に有効化
                return "Spike analysis will be available after MethodProfiler integration";
                // return CS1Profiler.Profiling.MethodProfiler.GetSpikeReport();
            }
            catch (Exception e)
            {
                return "Spike analysis error: " + e.Message;
            }
        }

        public static void OutputTopSlowMethods()
        {
            try
            {
                // TODO: MethodProfilerビルド成功後に有効化
                UnityEngine.Debug.Log("[CS1Profiler] Top slow methods functionality will be available after MethodProfiler integration");
                
                /*
                var topSlowReport = CS1Profiler.Profiling.MethodProfiler.GetTopSlowMethods(15);
                UnityEngine.Debug.Log("[CS1Profiler] " + topSlowReport);
                
                // 最も重いメソッドTOP5をCSVにも出力
                var stats = CS1Profiler.Profiling.MethodProfiler.GetStats();
                var topSlow = stats.Values.Where(m => m.CallCount > 0)
                                         .OrderByDescending(m => m.AverageMs)
                                         .Take(5);
                                         
                foreach (var method in topSlow)
                {
                    if (csvManager != null)
                    {
                        csvManager.QueueCsvWrite(
                            category: "TopSlow",
                            eventType: "SlowMethod",
                            durationMs: method.AverageMs,
                            count: method.CallCount,
                            memoryMB: 0,
                            rank: (int)(method.TotalMs / 1000), // 秒単位
                            description: $"{method.MethodName}|Max:{method.MaxMs:F1}ms|Total:{method.TotalMs:F0}ms"
                        );
                    }
                }
                */
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] OutputTopSlowMethods error: " + e.Message);
            }
        }

        public static CSVManager CsvManager
        {
            get { return csvManager; }
        }

        public static void Shutdown()
        {
            isProfilingEnabled = false;
        }
    }
}
