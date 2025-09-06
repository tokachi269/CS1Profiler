using HarmonyLib;
using System;
using UnityEngine;
using CS1Profiler.Managers;
using CS1Profiler.Profiling;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// 型安全なパッチ管理システム
    /// interface基盤でコンパイル時安全性を確保
    /// </summary>
    public static class PatchController
    {
        private const string HarmonyId = "me.cs1profiler.startup";
        private static bool _initialized = false;
        private static HarmonyLib.Harmony _harmony = null;

        // 各パッチプロバイダーのインスタンス（型安全）
        private static readonly PerformancePatchProvider _performanceProvider = new PerformancePatchProvider();
        private static readonly SimulationPatchProvider _simulationProvider = new SimulationPatchProvider();
        private static readonly LogSuppressionPatchProvider _logSuppressionProvider = new LogSuppressionPatchProvider();
        private static readonly StartupAnalysisPatchProvider _startupAnalysisProvider = new StartupAnalysisPatchProvider();
        private static readonly RenderItOptimizationPatchProvider _renderItOptimizationProvider = new RenderItOptimizationPatchProvider();
        private static readonly PloppableAsphaltFixOptimizationPatchProvider _ploppableAsphaltFixProvider = new PloppableAsphaltFixOptimizationPatchProvider();

        /// <summary>
        /// システム初期化
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) 
            {
                return;
            }

            UnityEngine.Debug.Log("[CS1Profiler] Initializing patch management system...");
            
            try
            {
                _harmony = new HarmonyLib.Harmony(HarmonyId);
                
                // デフォルト有効なパッチを適用
                ApplyDefaultPatches();
                
                _initialized = true;
                UnityEngine.Debug.Log("[CS1Profiler] Patch system initialized - Mode: Lightweight");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] Failed to initialize patch system: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// パフォーマンス測定パッチの有効/無効（型安全）
        /// MPSC Logger連携
        /// </summary>
        public static bool PerformanceProfilingEnabled
        {
            get => _performanceProvider.IsEnabled;
            set 
            {
                if (_initialized)
                {
                    SetPatchEnabled(_performanceProvider, value);
                    
                    // MPSC Logger連携
                    if (value)
                    {
                        CS1Profiler.Profiling.MPSCLogger.StartWriter();
                        UnityEngine.Debug.Log("[CS1Profiler] MPSC Performance profiling enabled");
                    }
                    else
                    {
                        CS1Profiler.Profiling.MPSCLogger.StopWriter();
                        UnityEngine.Debug.Log("[CS1Profiler] MPSC Performance profiling disabled");
                    }
                }
            }
        }

        /// <summary>
        /// シミュレーション測定パッチの有効/無効（型安全）
        /// </summary>
        public static bool SimulationProfilingEnabled
        {
            get => _simulationProvider.IsEnabled;
            set 
            {
                if (_initialized)
                    SetPatchEnabled(_simulationProvider, value);
            }
        }

        /// <summary>
        /// ログ抑制パッチの有効/無効（型安全）
        /// </summary>
        public static bool LogSuppressionEnabled
        {
            get => _logSuppressionProvider.IsEnabled;
            set 
            {
                if (_initialized)
                    SetPatchEnabled(_logSuppressionProvider, value);
            }
        }

        /// <summary>
        /// 起動解析パッチの有効/無効（型安全）
        /// </summary>
        public static bool StartupAnalysisEnabled
        {
            get => _startupAnalysisProvider.IsEnabled;
            set 
            {
                if (_initialized)
                    SetPatchEnabled(_startupAnalysisProvider, value);
            }
        }

        /// <summary>
        /// RenderIt最適化パッチの有効/無効（型安全）
        /// </summary>
        public static bool RenderItOptimizationEnabled
        {
            get => _renderItOptimizationProvider.IsEnabled;
            set 
            {
                if (_initialized)
                    SetPatchEnabled(_renderItOptimizationProvider, value);
            }
        }

        /// <summary>
        /// PloppableAsphaltFix最適化パッチの有効/無効（型安全）
        /// </summary>
        public static bool PloppableAsphaltFixOptimizationEnabled
        {
            get => _ploppableAsphaltFixProvider.IsEnabled;
            set 
            {
                if (_initialized)
                    SetPatchEnabled(_ploppableAsphaltFixProvider, value);
            }
        }

        /// <summary>
        /// 軽量モード判定（すべての測定系パッチがOFF）
        /// </summary>
        public static bool IsLightweightMode =>
            !_performanceProvider.IsEnabled && !_simulationProvider.IsEnabled;

        /// <summary>
        /// 軽量モードに設定（すべての測定系パッチをOFF）
        /// </summary>
        public static void EnableLightweightMode()
        {
            EnsureInitialized();
            
            UnityEngine.Debug.Log("[CS1Profiler] Switching to Lightweight Mode...");
            PerformanceProfilingEnabled = false;
            SimulationProfilingEnabled = false;
            UnityEngine.Debug.Log("[CS1Profiler] 🏃‍♂️ Lightweight Mode enabled - zero measurement overhead");
        }

        /// <summary>
        /// 解析モードに設定（すべての測定系パッチをON）
        /// </summary>
        public static void EnableAnalysisMode()
        {
            EnsureInitialized();
            
            UnityEngine.Debug.Log("[CS1Profiler] Switching to Analysis Mode...");
            PerformanceProfilingEnabled = true;
            SimulationProfilingEnabled = true;
            UnityEngine.Debug.Log("[CS1Profiler] 📊 Analysis Mode enabled - full measurement active");
        }

        /// <summary>
        /// システム状態を文字列で取得
        /// </summary>
        public static string GetStatusString()
        {
            EnsureInitialized();
            
            var performance = _performanceProvider.IsEnabled ? "ON" : "OFF";
            var simulation = _simulationProvider.IsEnabled ? "ON" : "OFF";
            var logSuppression = _logSuppressionProvider.IsEnabled ? "ON" : "OFF";
            var startupAnalysis = _startupAnalysisProvider.IsEnabled ? "ON" : "OFF";
            var mode = IsLightweightMode ? "Lightweight" : "Analysis";
            
            return $"Performance:{performance}, Simulation:{simulation}, LogSuppression:{logSuppression}, Startup:{startupAnalysis}, Mode:{mode}";
        }

        /// <summary>
        /// 全パッチを削除してシステム終了
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized) return;
            
            UnityEngine.Debug.Log("[CS1Profiler] Shutting down patch management system...");
            
            try
            {
                // 個別に無効化
                PerformanceProfilingEnabled = false;
                SimulationProfilingEnabled = false;
                LogSuppressionEnabled = false;
                StartupAnalysisEnabled = false;
                RenderItOptimizationEnabled = false;
                PloppableAsphaltFixOptimizationEnabled = false;
                
                _harmony?.UnpatchAll(HarmonyId);
                UnityEngine.Debug.Log("[CS1Profiler] All patches removed successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] Error during shutdown: {e.Message}");
            }
            
            _initialized = false;
        }

        /// <summary>
        /// パッチプロバイダーの有効/無効を設定（内部メソッド）
        /// </summary>
        private static void SetPatchEnabled(IPatchProvider provider, bool enabled)
        {
            // 初期化中は EnsureInitialized を呼ばない（無限ループ防止）
            if (!_initialized)
            {
                UnityEngine.Debug.LogWarning($"[CS1Profiler] Patch system not initialized, skipping {provider.Name} patch setting");
                return;
            }
            
            try
            {
                if (enabled && !provider.IsEnabled)
                {
                    provider.Enable(_harmony);
                    UnityEngine.Debug.Log($"[CS1Profiler] ✅ {provider.Name} patches enabled");
                }
                else if (!enabled && provider.IsEnabled)
                {
                    provider.Disable(_harmony);
                    UnityEngine.Debug.Log($"[CS1Profiler] ❌ {provider.Name} patches disabled");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] Failed to set {provider.Name} patches to {enabled}: {e.Message}");
            }
        }

        /// <summary>
        /// デフォルト有効なパッチを適用
        /// </summary>
        private static void ApplyDefaultPatches()
        {
            try
            {
                // デフォルト有効なパッチを直接有効化（setter経由を避ける）
                if (_logSuppressionProvider.DefaultEnabled)
                {
                    _logSuppressionProvider.Enable(_harmony);
                }
                    
                if (_startupAnalysisProvider.DefaultEnabled)
                {
                    _startupAnalysisProvider.Enable(_harmony);
                }
                
                if (_renderItOptimizationProvider.DefaultEnabled)
                {
                    _renderItOptimizationProvider.Enable(_harmony);
                }
                
                if (_ploppableAsphaltFixProvider.DefaultEnabled)
                {
                    _ploppableAsphaltFixProvider.Enable(_harmony);
                }
                
                UnityEngine.Debug.Log("[CS1Profiler] Default patches applied");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] Error applying default patches: {e.Message}");
            }
        }

        /// <summary>
        /// システムが初期化されていることを確認
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// システム状態をログ出力
        /// </summary>
        private static void LogSystemStatus()
        {
            UnityEngine.Debug.Log("[CS1Profiler] === Type-Safe Patch Management System Status ===");
            UnityEngine.Debug.Log($"[CS1Profiler] Performance: {(_performanceProvider.IsEnabled ? "ACTIVE" : "INACTIVE")} (Default: {(_performanceProvider.DefaultEnabled ? "ON" : "OFF")})");
            UnityEngine.Debug.Log($"[CS1Profiler] Simulation: {(_simulationProvider.IsEnabled ? "ACTIVE" : "INACTIVE")} (Default: {(_simulationProvider.DefaultEnabled ? "ON" : "OFF")})");
            UnityEngine.Debug.Log($"[CS1Profiler] LogSuppression: {(_logSuppressionProvider.IsEnabled ? "ACTIVE" : "INACTIVE")} (Default: {(_logSuppressionProvider.DefaultEnabled ? "ON" : "OFF")})");
            UnityEngine.Debug.Log($"[CS1Profiler] StartupAnalysis: {(_startupAnalysisProvider.IsEnabled ? "ACTIVE" : "INACTIVE")} (Default: {(_startupAnalysisProvider.DefaultEnabled ? "ON" : "OFF")})");
            UnityEngine.Debug.Log($"[CS1Profiler] Current Mode: {(IsLightweightMode ? "Lightweight" : "Analysis")}");
        }
    }
}
