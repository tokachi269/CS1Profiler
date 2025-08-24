using System;
using System.Collections.Generic;

namespace CS1Profiler.Profiling
{
    /// <summary>
    /// パフォーマンスプロファイリングのデータ構造
    /// </summary>
    public class SpikeInfo
    {
        public DateTime Timestamp { get; set; }
        public double ExecutionTimeMs { get; set; }
        public double AverageAtTime { get; set; }
        public double SpikeRatio => AverageAtTime > 0 ? ExecutionTimeMs / AverageAtTime : 0;
        public string CallStackInfo { get; set; }
    }

    public class ProfileData
    {
        public string MethodName { get; set; }
        public long TotalTicks { get; set; }
        public int CallCount { get; set; }
        public long MaxTicks { get; set; }
        public DateTime LastCall { get; set; }
        public List<SpikeInfo> Spikes { get; set; } = new List<SpikeInfo>();
        public int SpikeCount { get; set; }

        // 軽量システム用のプロパティ
        public double TotalMilliseconds { get; set; }
        public double AverageMilliseconds { get; set; }
        public double MaxMilliseconds { get; set; }
        public string AssemblyName { get; set; }
        public double MaxSpikeRatio { get; set; }

        // レガシー互換性のためのプロパティ
        public double TotalMs => TotalTicks > 0 ? TotalTicks / 10000.0 : TotalMilliseconds;
        public double AverageMs => CallCount > 0 ? TotalMs / CallCount : AverageMilliseconds;
        public double MaxMs => MaxTicks > 0 ? MaxTicks / 10000.0 : MaxMilliseconds;
    }
}
