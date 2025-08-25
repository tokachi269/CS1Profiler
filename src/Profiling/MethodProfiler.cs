using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace CS1Profiler.Profiling
{
    /// <summary>
    /// メソッド実行時間を計測するためのプロファイラー（ファサード）
    /// 機能を分離した複数のクラスを統合管理
    /// </summary>
    public static class MethodProfiler
    {
        private static bool _isInitialized = false;
        private static bool _isEnabled = true;
        
        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            if (_isInitialized)
            {
                UnityEngine.Debug.LogWarning("[CS1Profiler] MethodProfiler already initialized");
                return;
            }
            
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] MethodProfiler initializing...");
                
                // MODとメソッド検出、パッチング
                MethodPatcher.Initialize(harmony);
                
                _isInitialized = true;
                UnityEngine.Debug.Log("[CS1Profiler] MethodProfiler initialization completed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] MethodProfiler.Initialize error: {e.Message}");
            }
        }

        public static void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            UnityEngine.Debug.Log($"[CS1Profiler] MethodProfiler enabled: {enabled}");
        }

        public static bool IsEnabled()
        {
            return _isEnabled;
        }

        public static void PrintDetailedStats()
        {
            try
            {
                var stats = PerformanceProfiler.GetDetailedStats();
                UnityEngine.Debug.Log("[CS1Profiler] === Detailed Performance Stats ===");
                foreach (var stat in stats.Take(20))
                {
                    UnityEngine.Debug.Log($"[CS1Profiler] {stat.Key}: {stat.Value}ms avg");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] PrintDetailedStats error: {e.Message}");
            }
        }
        
        // レガシー互換性のために残すメソッド
        public static void MethodStart(MethodBase method)
        {
            if (!_isEnabled) return;
            PerformanceProfiler.MethodStart(method);
            
            // スパイク検出も実行
            if (method != null)
            {
                string methodKey = method.DeclaringType?.Name + "." + method.Name;
                SpikeDetector.StartMethod(methodKey);
            }
        }
        
        public static void MethodEnd(MethodBase method)
        {
            PerformanceProfiler.MethodEnd();
            
            // スパイク検出も実行
            if (method != null)
            {
                string methodKey = method.DeclaringType?.Name + "." + method.Name;
                SpikeDetector.EndMethod(methodKey);
            }
        }
        
        public static Dictionary<string, ProfileData> GetStats()
        {
            // パフォーマンスプロファイラーの結果をレガシー形式に変換
            var topMethods = PerformanceProfiler.GetTopMethods(50);
            var result = new Dictionary<string, ProfileData>();
            
            foreach (var method in topMethods)
            {
                result[method.MethodName] = method;
            }
            
            return result;
        }
        
        public static List<ProfileData> GetMethodsWithSpikes()
        {
            return SpikeDetector.GetMethodsWithSpikes();
        }
        
        public static List<SpikeInfo> GetRecentSpikes(int minutes = 5)
        {
            return SpikeDetector.GetRecentSpikes(minutes);
        }
        
        public static string GetSpikeReport()
        {
            var methodsWithSpikes = GetMethodsWithSpikes();
            if (!methodsWithSpikes.Any())
            {
                return "No performance spikes detected.";
            }
            
            var report = $"=== PERFORMANCE SPIKE REPORT ===\n";
            report += $"Found {methodsWithSpikes.Count} methods with performance spikes:\n\n";
            
            foreach (var method in methodsWithSpikes.Take(10))
            {
                report += $"• {method.MethodName}\n";
                report += $"  Spikes: {method.SpikeCount}, Max ratio: {method.MaxSpikeRatio:F1}x\n";
                var recentSpike = method.Spikes.LastOrDefault();
                if (recentSpike != null)
                {
                    report += $"  Last spike: {recentSpike.Timestamp:HH:mm:ss} " +
                             $"({recentSpike.ExecutionTimeMs:F2}ms, {recentSpike.SpikeRatio:F1}x avg)\n";
                }
                report += "\n";
            }
            
            return report;
        }
        
        public static string GetTopSlowMethods(int count = 10)
        {
            var allMethods = PerformanceProfiler.GetTopMethods(count);
            if (!allMethods.Any())
            {
                return "No method data available.";
            }
            
            var report = $"=== TOP {count} SLOWEST METHODS ===\n";
            for (int i = 0; i < allMethods.Count; i++)
            {
                var method = allMethods[i];
                report += $"{i+1}. {method.MethodName} ({method.AssemblyName})\n";
                report += $"   Average: {method.AverageMilliseconds:F2}ms, Max: {method.MaxMilliseconds:F2}ms\n";
                report += $"   Calls: {method.CallCount}, Total: {method.TotalMilliseconds:F1}ms\n\n";
            }
            
            return report;
        }
        
        public static void Reset()
        {
            PerformanceProfiler.Reset();
            SpikeDetector.Reset();
        }

        // 要件対応: CSV出力用のメソッドを追加
        public static string GetCSVReportTopN(int topN)
        {
            var topMethods = PerformanceProfiler.GetTopMethods(topN);
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Rank,Method,AvgMs,MaxMs,TotalMs,Calls");
            
            for (int i = 0; i < topMethods.Count; i++)
            {
                var method = topMethods[i];
                csv.AppendLine(string.Format("{0},{1},{2:F3},{3:F3},{4:F3},{5}",
                    i + 1, method.MethodName, method.AverageMilliseconds, 
                    method.MaxMilliseconds, method.TotalMilliseconds, method.CallCount));
            }
            
            return csv.ToString();
        }

        // 軽量版: 生データをそのまま出力（外部で集計処理）
        public static string GetCSVReportAll()
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Timestamp,Method,ExecutionTicks");
            
            // 生データを直接出力するため、PerformanceProfilerから生データを取得
            return PerformanceProfiler.GetRawDataCSV();
        }

        // 要件対応: 統計をクリア
        public static void Clear()
        {
            Reset();
            UnityEngine.Debug.Log("[CS1Profiler] MethodProfiler statistics cleared");
        }
        
        // パッチ情報取得
        public static int GetPatchedMethodCount()
        {
            return MethodPatcher.GetPatchedMethodCount();
        }
        
        // MOD検出結果
        public static bool IsFromDetectedMod(string methodKey)
        {
            return MethodPatcher.IsFromDetectedMod(methodKey);
        }
    }
}
