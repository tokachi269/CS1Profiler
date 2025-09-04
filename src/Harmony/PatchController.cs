using System;
using UnityEngine;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// 全Harmonyパッチの有効/無効を統一管理するクラス
    /// パフォーマンス重視のため、通常時はOFF、必要時のみONにする
    /// </summary>
    public static class PatchController
    {
        // 各パッチカテゴリのON/OFF状態
        private static bool _performanceProfilingEnabled = false; // デフォルトOFF（パフォーマンス重視）
        private static bool _startupAnalysisEnabled = true;       // 起動解析はON
        private static bool _logSuppressionEnabled = true;        // ログ抑制はON
        private static bool _simulationProfilingEnabled = false;  // SimulationステップもOFF

        /// <summary>
        /// パフォーマンス測定パッチの有効/無効（デフォルト：無効）
        /// </summary>
        public static bool PerformanceProfilingEnabled
        {
            get => _performanceProfilingEnabled;
            set
            {
                if (_performanceProfilingEnabled != value)
                {
                    _performanceProfilingEnabled = value;
                    LogStateChange("PerformanceProfiling", value);
                    
                    // CSVに状態変更を記録
                    RecordStateChange("PerformanceProfiling", value);
                }
            }
        }

        /// <summary>
        /// 起動時解析パッチの有効/無効（デフォルト：有効）
        /// </summary>
        public static bool StartupAnalysisEnabled
        {
            get => _startupAnalysisEnabled;
            set
            {
                if (_startupAnalysisEnabled != value)
                {
                    _startupAnalysisEnabled = value;
                    LogStateChange("StartupAnalysis", value);
                    RecordStateChange("StartupAnalysis", value);
                }
            }
        }

        /// <summary>
        /// ログ抑制パッチの有効/無効（デフォルト：有効）
        /// </summary>
        public static bool LogSuppressionEnabled
        {
            get => _logSuppressionEnabled;
            set
            {
                if (_logSuppressionEnabled != value)
                {
                    _logSuppressionEnabled = value;
                    LogStateChange("LogSuppression", value);
                    RecordStateChange("LogSuppression", value);
                }
            }
        }

        /// <summary>
        /// Simulationステップ測定の有効/無効（デフォルト：無効）
        /// </summary>
        public static bool SimulationProfilingEnabled
        {
            get => _simulationProfilingEnabled;
            set
            {
                if (_simulationProfilingEnabled != value)
                {
                    _simulationProfilingEnabled = value;
                    LogStateChange("SimulationProfiling", value);
                    RecordStateChange("SimulationProfiling", value);
                }
            }
        }

        /// <summary>
        /// 全てのプロファイリングを一括でON/OFF
        /// </summary>
        public static void SetAllProfilingEnabled(bool enabled)
        {
            UnityEngine.Debug.Log($"[CS1Profiler] Setting ALL profiling to: {(enabled ? "ENABLED" : "DISABLED")}");
            
            PerformanceProfilingEnabled = enabled;
            SimulationProfilingEnabled = enabled;
            
            // 起動解析とログ抑制は別管理（常にONが推奨）
        }

        /// <summary>
        /// 測定系のみを一括でON/OFF（起動解析・ログ抑制は除く）
        /// </summary>
        public static void SetMeasurementEnabled(bool enabled)
        {
            UnityEngine.Debug.Log($"[CS1Profiler] Setting measurement profiling to: {(enabled ? "ENABLED" : "DISABLED")}");
            
            PerformanceProfilingEnabled = enabled;
            SimulationProfilingEnabled = enabled;
        }

        /// <summary>
        /// 現在の状態をすべて取得
        /// </summary>
        public static string GetCurrentStatus()
        {
            return $"Performance: {(_performanceProfilingEnabled ? "ON" : "OFF")}, " +
                   $"Startup: {(_startupAnalysisEnabled ? "ON" : "OFF")}, " +
                   $"LogSuppression: {(_logSuppressionEnabled ? "ON" : "OFF")}, " +
                   $"Simulation: {(_simulationProfilingEnabled ? "ON" : "OFF")}";
        }

        /// <summary>
        /// 軽量モード：測定系を全てOFFにしてパフォーマンス重視
        /// </summary>
        public static void EnableLightweightMode()
        {
            UnityEngine.Debug.Log("[CS1Profiler] Enabling lightweight mode (all measurements OFF)");
            SetMeasurementEnabled(false);
        }

        /// <summary>
        /// 解析モード：測定系を全てONにして詳細分析
        /// </summary>
        public static void EnableAnalysisMode()
        {
            UnityEngine.Debug.Log("[CS1Profiler] Enabling analysis mode (all measurements ON)");
            SetMeasurementEnabled(true);
        }

        private static void LogStateChange(string category, bool enabled)
        {
            UnityEngine.Debug.Log($"[CS1Profiler] {category} profiling: {(enabled ? "ENABLED" : "DISABLED")}");
        }

        private static void RecordStateChange(string category, bool enabled)
        {
            try
            {
                if (CS1Profiler.Managers.ProfilerManager.Instance?.CsvManager != null)
                {
                    CS1Profiler.Managers.ProfilerManager.Instance.CsvManager.QueueCsvWrite(
                        "System", 
                        $"{category}Toggle", 
                        0, 0, 0, 0, 
                        enabled ? "ENABLED" : "DISABLED"
                    );
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] Failed to record state change: {e.Message}");
            }
        }

        /// <summary>
        /// 初期化時の状態ログ
        /// </summary>
        public static void LogInitialState()
        {
            UnityEngine.Debug.Log($"[CS1Profiler] PatchController initialized - {GetCurrentStatus()}");
        }
    }
}
