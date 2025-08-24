using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace CS1Profiler.Profiling
{
    /// <summary>
    /// スパイク検出とパフォーマンス異常の検出
    /// </summary>
    public static class SpikeDetector
    {
        private const double SPIKE_THRESHOLD_RATIO = 3.0; // 平均の3倍以上でスパイクとみなす
        private const int MIN_CALLS_FOR_SPIKE_DETECTION = 10; // スパイク検出に必要な最小呼び出し回数
        private const int MAX_SPIKES_PER_METHOD = 20; // メソッドごとの最大スパイク記録数（削減）
        
        private static readonly Dictionary<string, ProfileData> _methodStats = new Dictionary<string, ProfileData>();
        private static readonly Dictionary<string, Stopwatch> _activeStopwatches = new Dictionary<string, Stopwatch>();
        
        public static void StartMethod(string methodKey)
        {
            if (!_methodStats.ContainsKey(methodKey))
            {
                _methodStats[methodKey] = new ProfileData { MethodName = methodKey };
            }
            
            if (!_activeStopwatches.ContainsKey(methodKey))
            {
                _activeStopwatches[methodKey] = new Stopwatch();
            }
            
            _activeStopwatches[methodKey].Reset();
            _activeStopwatches[methodKey].Start();
        }
        
        public static void EndMethod(string methodKey)
        {
            if (!_activeStopwatches.ContainsKey(methodKey) || !_methodStats.ContainsKey(methodKey))
                return;
                
            var stopwatch = _activeStopwatches[methodKey];
            stopwatch.Stop();
            
            var stats = _methodStats[methodKey];
            double executionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            
            stats.TotalTicks += stopwatch.ElapsedTicks;
            stats.CallCount++;
            stats.LastCall = DateTime.Now;
            
            if (stopwatch.ElapsedTicks > stats.MaxTicks)
                stats.MaxTicks = stopwatch.ElapsedTicks;
                
            // スパイク検出
            if (stats.CallCount >= MIN_CALLS_FOR_SPIKE_DETECTION)
            {
                double currentAverage = stats.AverageMs;
                if (executionTimeMs > currentAverage * SPIKE_THRESHOLD_RATIO)
                {
                    DetectSpike(stats, executionTimeMs, currentAverage);
                }
            }
        }
        
        private static void DetectSpike(ProfileData stats, double executionTimeMs, double currentAverage)
        {
            if (stats.Spikes.Count >= MAX_SPIKES_PER_METHOD)
            {
                stats.Spikes.RemoveAt(0); // 古いスパイクを削除
            }
            
            var spike = new SpikeInfo
            {
                Timestamp = DateTime.Now,
                ExecutionTimeMs = executionTimeMs,
                AverageAtTime = currentAverage,
                CallStackInfo = GetSimpleCallStack()
            };
            
            stats.Spikes.Add(spike);
            stats.SpikeCount++;
            
            // 最大スパイク比を更新
            if (spike.SpikeRatio > stats.MaxSpikeRatio)
                stats.MaxSpikeRatio = spike.SpikeRatio;
                
            // 重要なスパイクのみログ出力（5倍以上）
            if (spike.SpikeRatio >= 5.0)
            {
                UnityEngine.Debug.LogWarning($"[CS1Profiler] SPIKE DETECTED: {stats.MethodName} " +
                    $"took {executionTimeMs:F2}ms ({spike.SpikeRatio:F1}x average)");
            }
        }
        
        private static string GetSimpleCallStack()
        {
            try
            {
                var stackTrace = new StackTrace(true);
                var frames = stackTrace.GetFrames();
                if (frames != null && frames.Length > 3)
                {
                    var frame = frames[3]; // Skip SpikeDetector methods
                    return frame.GetMethod()?.DeclaringType?.Name ?? "Unknown";
                }
            }
            catch { }
            return "Unknown";
        }
        
        public static List<ProfileData> GetMethodsWithSpikes()
        {
            var result = new List<ProfileData>();
            foreach (var kvp in _methodStats)
            {
                if (kvp.Value.SpikeCount > 0)
                {
                    result.Add(kvp.Value);
                }
            }
            result.Sort((a, b) => b.SpikeCount.CompareTo(a.SpikeCount));
            return result;
        }
        
        public static List<SpikeInfo> GetRecentSpikes(int minutes = 5)
        {
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            var recentSpikes = new List<SpikeInfo>();
            
            foreach (var stats in _methodStats.Values)
            {
                foreach (var spike in stats.Spikes)
                {
                    if (spike.Timestamp > cutoff)
                    {
                        recentSpikes.Add(spike);
                    }
                }
            }
            
            recentSpikes.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
            return recentSpikes;
        }
        
        public static void Reset()
        {
            _methodStats.Clear();
            _activeStopwatches.Clear();
        }
    }
}
