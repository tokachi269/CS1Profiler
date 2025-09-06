using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CS1Profiler.Core;

namespace CS1Profiler.Profiling
{
    /// <summary>
    /// CSVデータの統計解析と集計を行うクラス
    /// </summary>
    public class CsvAnalyzer
    {
        public class PerformanceRecord
        {
            public DateTime DateTime { get; set; }
            public int FrameCount { get; set; }
            public string Category { get; set; }
            public string EventType { get; set; }
            public double DurationMs { get; set; }
            public int Count { get; set; }
            public double MemoryMB { get; set; }
            public string Description { get; set; }
            
            // 計算用追加フィールド
            public double TotalDurationPerFrame => DurationMs * Count;
        }

        public class MethodStatistics
        {
            public string MethodName { get; set; }
            public string Category { get; set; }
            
            // 基本統計
            public int TotalCalls { get; set; }
            public double AverageDurationMs { get; set; }
            public double MaxDurationMs { get; set; }
            public double MinDurationMs { get; set; }
            public double StandardDeviation { get; set; }
            
            // フレーム統計
            public int FramesActive { get; set; }
            public double AverageTotalDurationPerFrame { get; set; }
            public double MaxTotalDurationPerFrame { get; set; }
            
            // スパイク解析
            public int SpikeCount { get; set; }
            public double SpikeThreshold { get; set; }
            public List<PerformanceRecord> SpikeRecords { get; set; } = new List<PerformanceRecord>();
            
            // 呼び出し回数変動
            public int MinCallsPerFrame { get; set; }
            public int MaxCallsPerFrame { get; set; }
            public double AverageCallsPerFrame { get; set; }
            
            // メモリ統計
            public double AverageMemoryMB { get; set; }
            public double MaxMemoryMB { get; set; }
        }

        public class FrameStatistics
        {
            public int FrameNumber { get; set; }
            public DateTime FrameTime { get; set; }
            public double TotalFrameDurationMs { get; set; }
            public int TotalMethodCalls { get; set; }
            public double TotalMemoryMB { get; set; }
            public List<PerformanceRecord> TopMethods { get; set; } = new List<PerformanceRecord>();
        }

        private List<PerformanceRecord> _records = new List<PerformanceRecord>();

        /// <summary>
        /// CSVファイルを読み込んで解析準備
        /// </summary>
        public bool LoadCsvFile(string filePath)
        {
            try
            {
                _records.Clear();
                var lines = File.ReadAllLines(filePath);
                
                if (lines.Length <= 1) return false; // ヘッダーのみの場合
                
                // ヘッダー行をスキップして解析
                for (int i = 1; i < lines.Length; i++)
                {
                    var record = ParseCsvLine(lines[i]);
                    if (record != null)
                    {
                        _records.Add(record);
                    }
                }
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} CSV loaded: {_records.Count} records from {filePath}");
                return _records.Count > 0;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} CSV load failed: {e.Message}");
                return false;
            }
        }

        private PerformanceRecord ParseCsvLine(string line)
        {
            try
            {
                var parts = line.Split(',');
                if (parts.Length < 7) return null;
                
                return new PerformanceRecord
                {
                    DateTime = DateTime.Parse(parts[0]),
                    FrameCount = int.Parse(parts[1]),
                    Category = parts[2].Trim('"'),
                    EventType = parts[3].Trim('"'),
                    DurationMs = double.Parse(parts[4], CultureInfo.InvariantCulture),
                    Count = int.Parse(parts[5]),
                    MemoryMB = double.Parse(parts[6], CultureInfo.InvariantCulture),
                    Description = parts.Length > 7 ? parts[7].Trim('"') : ""
                };
            }
            catch
            {
                return null; // 解析失敗した行はスキップ
            }
        }

        /// <summary>
        /// メソッド別統計情報を生成
        /// </summary>
        public List<MethodStatistics> GenerateMethodStatistics(double spikeThresholdMultiplier = 2.0)
        {
            var methodGroups = _records.GroupBy(r => r.Description).ToList();
            var statistics = new List<MethodStatistics>();

            foreach (var group in methodGroups)
            {
                var records = group.ToList();
                if (records.Count == 0) continue;

                var durations = records.Select(r => r.DurationMs).ToList();
                var average = durations.Average();
                var spikeThreshold = average * spikeThresholdMultiplier;
                
                var frameGroups = records.GroupBy(r => r.FrameCount).ToList();
                var frameTotals = frameGroups.Select(fg => fg.Sum(r => r.TotalDurationPerFrame)).ToList();
                var frameCalls = frameGroups.Select(fg => fg.Sum(r => r.Count)).ToList();

                var stat = new MethodStatistics
                {
                    MethodName = group.Key,
                    Category = records.First().Category,
                    
                    // 基本統計
                    TotalCalls = records.Sum(r => r.Count),
                    AverageDurationMs = average,
                    MaxDurationMs = durations.Max(),
                    MinDurationMs = durations.Min(),
                    StandardDeviation = CalculateStandardDeviation(durations, average),
                    
                    // フレーム統計
                    FramesActive = frameGroups.Count,
                    AverageTotalDurationPerFrame = frameTotals.Count > 0 ? frameTotals.Average() : 0,
                    MaxTotalDurationPerFrame = frameTotals.Count > 0 ? frameTotals.Max() : 0,
                    
                    // スパイク解析
                    SpikeThreshold = spikeThreshold,
                    SpikeRecords = records.Where(r => r.DurationMs > spikeThreshold).ToList(),
                    
                    // 呼び出し回数変動
                    MinCallsPerFrame = frameCalls.Count > 0 ? frameCalls.Min() : 0,
                    MaxCallsPerFrame = frameCalls.Count > 0 ? frameCalls.Max() : 0,
                    AverageCallsPerFrame = frameCalls.Count > 0 ? frameCalls.Average() : 0,
                    
                    // メモリ統計
                    AverageMemoryMB = records.Average(r => r.MemoryMB),
                    MaxMemoryMB = records.Max(r => r.MemoryMB)
                };
                
                stat.SpikeCount = stat.SpikeRecords.Count;
                statistics.Add(stat);
            }

            return statistics.OrderByDescending(s => s.AverageTotalDurationPerFrame).ToList();
        }

        /// <summary>
        /// フレーム別統計情報を生成
        /// </summary>
        public List<FrameStatistics> GenerateFrameStatistics(int topMethodCount = 5)
        {
            var frameGroups = _records.GroupBy(r => r.FrameCount).ToList();
            var statistics = new List<FrameStatistics>();

            foreach (var frameGroup in frameGroups.OrderBy(g => g.Key))
            {
                var frameRecords = frameGroup.ToList();
                var topMethods = frameRecords.OrderByDescending(r => r.TotalDurationPerFrame)
                                           .Take(topMethodCount)
                                           .ToList();

                var frameStat = new FrameStatistics
                {
                    FrameNumber = frameGroup.Key,
                    FrameTime = frameRecords.First().DateTime,
                    TotalFrameDurationMs = frameRecords.Sum(r => r.TotalDurationPerFrame),
                    TotalMethodCalls = frameRecords.Sum(r => r.Count),
                    TotalMemoryMB = frameRecords.Sum(r => r.MemoryMB),
                    TopMethods = topMethods
                };

                statistics.Add(frameStat);
            }

            return statistics;
        }

        /// <summary>
        /// カテゴリ別集計を生成
        /// </summary>
        public Dictionary<string, MethodStatistics> GenerateCategoryStatistics()
        {
            var categoryGroups = _records.GroupBy(r => r.Category);
            var categoryStats = new Dictionary<string, MethodStatistics>();

            foreach (var categoryGroup in categoryGroups)
            {
                var records = categoryGroup.ToList();
                var durations = records.Select(r => r.DurationMs).ToList();
                var average = durations.Average();

                categoryStats[categoryGroup.Key] = new MethodStatistics
                {
                    MethodName = $"[Category] {categoryGroup.Key}",
                    Category = categoryGroup.Key,
                    TotalCalls = records.Sum(r => r.Count),
                    AverageDurationMs = average,
                    MaxDurationMs = durations.Max(),
                    MinDurationMs = durations.Min(),
                    StandardDeviation = CalculateStandardDeviation(durations, average),
                    AverageMemoryMB = records.Average(r => r.MemoryMB),
                    MaxMemoryMB = records.Max(r => r.MemoryMB)
                };
            }

            return categoryStats;
        }

        /// <summary>
        /// 統計結果をCSVファイルに出力
        /// </summary>
        public void ExportStatistics(string outputPath, List<MethodStatistics> methodStats)
        {
            try
            {
                using (var writer = new StreamWriter(outputPath))
                {
                    // ヘッダー
                    writer.WriteLine("MethodName,Category,TotalCalls,AvgDurationMs,MaxDurationMs,StdDev,FramesActive,AvgTotalPerFrame,MaxTotalPerFrame,SpikeCount,SpikeThreshold,AvgCallsPerFrame,MaxCallsPerFrame,AvgMemoryMB,MaxMemoryMB");
                    
                    // データ
                    foreach (var stat in methodStats)
                    {
                        writer.WriteLine($"\"{stat.MethodName}\",\"{stat.Category}\",{stat.TotalCalls},{stat.AverageDurationMs:F3},{stat.MaxDurationMs:F3},{stat.StandardDeviation:F3},{stat.FramesActive},{stat.AverageTotalDurationPerFrame:F3},{stat.MaxTotalDurationPerFrame:F3},{stat.SpikeCount},{stat.SpikeThreshold:F3},{stat.AverageCallsPerFrame:F2},{stat.MaxCallsPerFrame},{stat.AverageMemoryMB:F2},{stat.MaxMemoryMB:F2}");
                    }
                }
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Statistics exported to: {outputPath}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Export failed: {e.Message}");
            }
        }

        private static double CalculateStandardDeviation(List<double> values, double average)
        {
            if (values.Count <= 1) return 0;
            
            var sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));
            return Math.Sqrt(sumOfSquares / (values.Count - 1));
        }

        /// <summary>
        /// デバッグ用：統計サマリーを表示
        /// </summary>
        public void PrintStatisticsSummary(List<MethodStatistics> methodStats, int topCount = 10)
        {
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} === Performance Statistics Summary (Top {topCount}) ===");
            
            var topMethods = methodStats.Take(topCount).ToList();
            
            foreach (var stat in topMethods)
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {stat.MethodName}:");
                UnityEngine.Debug.Log($"  Total: {stat.AverageTotalDurationPerFrame:F2}ms/frame, Calls: {stat.TotalCalls}, Spikes: {stat.SpikeCount}");
                UnityEngine.Debug.Log($"  Avg: {stat.AverageDurationMs:F3}ms, Max: {stat.MaxDurationMs:F3}ms, StdDev: {stat.StandardDeviation:F3}ms");
            }
        }
    }
}
