using System;
using UnityEngine;

namespace CS1Profiler
{
    /// <summary>
    /// CPU側のホットスポット特定用プロファイラー
    /// Unity 5.6での安定性のため現在は無効化
    /// </summary>
    public class HotspotProfiler
    {
        // 設定
        public static int ReportIntervalFrames = 120; // 2秒間隔（60FPS基準）
        public static int TopMethodsToReport = 20;
        public static bool IsEnabled = false;

        /// <summary>
        /// ホットスポットプロファイリングを開始（Unity 5.6では無効化）
        /// </summary>
        public static void StartProfiling()
        {
            // Unity 5.6での安定性のため、現在は無効化
            Debug.Log("[CS1Profiler] CPU Hotspot Profiling is disabled for stability in Unity 5.6");
            IsEnabled = false;
        }

        /// <summary>
        /// ホットスポットプロファイリングを停止
        /// </summary>
        public static void StopProfiling()
        {
            IsEnabled = false;
            Debug.Log("[CS1Profiler] CPU Hotspot Profiling stopped");
        }

        /// <summary>
        /// レポート出力（現在は無効）
        /// </summary>
        public static void GenerateReport()
        {
            if (!IsEnabled) return;
            // Unity 5.6では無効化
        }

        /// <summary>
        /// 統計クリア
        /// </summary>
        public static void ClearStatistics()
        {
            // Unity 5.6では無効化
        }
    }
}
