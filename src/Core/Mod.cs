// Cities: Skylines (CS1) 用：メインMODクラス
// アーキテクチャ: CameraOperatorMod方式でインスタンス再生成対応

using ICities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CS1Profiler
{
    /// <summary>
    /// Cities Skylines MOD メインクラス - ProfilerManager分離版
    /// </summary>
    public sealed class Mod : LoadingExtensionBase, IUserMod
    {
        // GameObject管理（CameraOperatorMod方式）
        private GameObject _profilerManagerObject;
        private const string ProfilerManagerName = "CS1ProfilerManager";
        
        // 起動時解析用の追加フィールド
        private static DateTime _gameStartTime = DateTime.MinValue;
        private static bool _startupProfilingActive = false;
        private static readonly List<string> _startupLog = new List<string>();

        // IUserMod必須プロパティ
        public string Name 
        { 
            get { return "CS1 Startup Performance Analyzer"; } 
        }
        
        public string Description 
        { 
            get { return "Detailed startup and loading performance profiler for Cities: Skylines"; } 
        }

        public void OnEnabled()
        {
            UnityEngine.Debug.Log("[CS1Profiler] === MOD OnEnabled (ProfilerManager Architecture) ===");
            
            // 起動時解析の開始
            if (_gameStartTime == DateTime.MinValue)
            {
                _gameStartTime = DateTime.Now;
                _startupProfilingActive = true;
                LogStartupEvent("MOD_ENABLED", "CS1Profiler mod enabled and startup analysis started");
            }
            
            InitializeProfilerManager();
            UnityEngine.Debug.Log("[CS1Profiler] === MOD OnEnabled COMPLETED ===");
        }

        public void OnDisabled()
        {
            UnityEngine.Debug.Log("[CS1Profiler] === MOD OnDisabled ===");
            DestroyProfilerManager();
            UnityEngine.Debug.Log("[CS1Profiler] === MOD OnDisabled COMPLETED ===");
        }

        // ProfilerManager初期化
        private void InitializeProfilerManager()
        {
            try
            {
                // 既存のProfilerManagerを検索して削除
                GameObject existingManager = GameObject.Find(ProfilerManagerName);
                if (existingManager != null)
                {
                    UnityEngine.Debug.Log("[CS1Profiler] Found existing ProfilerManager, destroying...");
                    UnityEngine.Object.Destroy(existingManager);
                }

                // 新しいProfilerManagerを作成
                _profilerManagerObject = new GameObject(ProfilerManagerName);
                ProfilerManager.Instance = _profilerManagerObject.AddComponent<ProfilerManager>();
                ProfilerManager.Instance.Initialize();
                
                UnityEngine.Debug.Log("[CS1Profiler] ProfilerManager initialized successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ProfilerManager initialization failed: " + e.Message);
                UnityEngine.Debug.LogError("[CS1Profiler] Stack trace: " + e.StackTrace);
            }
        }

        // ProfilerManager破棄
        private void DestroyProfilerManager()
        {
            try
            {
                if (_profilerManagerObject != null)
                {
                    UnityEngine.Debug.Log("[CS1Profiler] Destroying ProfilerManager...");
                    UnityEngine.Object.Destroy(_profilerManagerObject);
                    _profilerManagerObject = null;
                }
                
                ProfilerManager.Instance = null;
                UnityEngine.Debug.Log("[CS1Profiler] ProfilerManager destroyed successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ProfilerManager destruction error: " + e.Message);
            }
        }

        // LoadingExtensionBase オーバーライド（起動解析機能付き）
        public override void OnLevelLoaded(LoadMode mode)
        {
            LogStartupEvent("LEVEL_LOADED", "Level loaded - Mode: " + mode.ToString());
            UnityEngine.Debug.Log("[CS1Profiler] OnLevelLoaded - Mode: " + mode);
            
            // ゲーム完全起動後の解析レポート生成
            if (_startupProfilingActive)
            {
                GenerateStartupReport();
                _startupProfilingActive = false;
            }
            
            // ProfilerManager初期化（レベル読み込み時にも確認）
            if (ProfilerManager.Instance == null)
            {
                InitializeProfilerManager();
            }
        }

        public override void OnLevelUnloading()
        {
            LogStartupEvent("LEVEL_UNLOADING", "Level unloading started");
            UnityEngine.Debug.Log("[CS1Profiler] OnLevelUnloading");
        }

        public override void OnCreated(ILoading loading)
        {
            LogStartupEvent("LOADING_CREATED", "Loading manager created");
            UnityEngine.Debug.Log("[CS1Profiler] OnCreated");
        }

        public override void OnReleased()
        {
            LogStartupEvent("LOADING_RELEASED", "Loading manager released - cleanup initiated");
            UnityEngine.Debug.Log("[CS1Profiler] OnReleased - Complete cleanup");
            DestroyProfilerManager();
        }

        // 起動イベントログ記録機能（StartupHooksから呼び出すためpublic static）
        public static void LogStartupEvent(string eventType, string description)
        {
            if (!_startupProfilingActive) return;
            
            try
            {
                DateTime now = DateTime.Now;
                TimeSpan elapsed = now - _gameStartTime;
                string logEntry = string.Format("[{0:HH:mm:ss.fff}] +{1:F3}s | {2} | {3}", 
                    now, elapsed.TotalSeconds, eventType, description);
                
                _startupLog.Add(logEntry);
                UnityEngine.Debug.Log("[CS1Profiler-Startup] " + logEntry);
                
                // ProfilerManager経由でCSVに記録
                if (ProfilerManager.Instance != null && ProfilerManager.Instance.CsvManager != null)
                {
                    long memoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                    ProfilerManager.Instance.CsvManager.QueueCsvWrite("Startup", eventType, elapsed.TotalMilliseconds, 1, memoryMB, 0, description);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LogStartupEvent error: " + e.Message);
            }
        }

        // 起動解析レポート生成
        private static void GenerateStartupReport()
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] === STARTUP ANALYSIS REPORT ===");
                
                DateTime endTime = DateTime.Now;
                TimeSpan totalStartupTime = endTime - _gameStartTime;
                
                UnityEngine.Debug.Log("[CS1Profiler] Total startup time: " + totalStartupTime.TotalSeconds.ToString("F2") + " seconds");
                UnityEngine.Debug.Log("[CS1Profiler] Startup events captured: " + _startupLog.Count);
                
                // 詳細ログをまとめて出力
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("=== DETAILED STARTUP TIMELINE ===");
                for (int i = 0; i < _startupLog.Count; i++)
                {
                    sb.AppendLine(_startupLog[i]);
                }
                sb.AppendLine("=== END STARTUP TIMELINE ===");
                
                UnityEngine.Debug.Log(sb.ToString());
                
                // ProfilerManager経由でCSV統計を保存
                if (ProfilerManager.Instance != null && ProfilerManager.Instance.CsvManager != null)
                {
                    ProfilerManager.Instance.CsvManager.QueueCsvWrite("StartupSummary", "TotalTime", totalStartupTime.TotalMilliseconds, 
                        _startupLog.Count, GC.GetTotalMemory(false) / 1024 / 1024, 0, 
                        "Complete startup analysis - " + _startupLog.Count + " events tracked");
                }
                
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] GenerateStartupReport error: " + e.Message);
            }
        }
        
        // オプション画面（簡素化版）
        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] === OnSettingsUI CALLED ===");
                
                var group = helper.AddGroup("CS1 Profiler - Performance Monitor");
                
                // 基本情報表示
                group.AddSpace(5);
                var statusGroup = group.AddGroup("Status");
                
                string profilingStatus = "Unknown";
                string csvPath = "Not initialized";
                
                if (ProfilerManager.Instance != null)
                {
                    profilingStatus = ProfilerManager.Instance.IsProfilingEnabled() ? "🟢 ACTIVE" : "🔴 STOPPED";
                    csvPath = ProfilerManager.Instance.GetCsvPath();
                    if (csvPath.Length > 60) csvPath = "..." + csvPath.Substring(csvPath.Length - 57);
                }
                
                statusGroup.AddTextfield("Profiling Status:", profilingStatus, null);
                statusGroup.AddTextfield("CSV Output:", csvPath, null);
                statusGroup.AddTextfield("Current Time:", DateTime.Now.ToString("HH:mm:ss.fff"), null);
                
                // キーボード操作説明
                group.AddSpace(10);
                var controlsGroup = group.AddGroup("Keyboard Controls");
                controlsGroup.AddTextfield("Toggle Profiling:", "Press P key", null);
                controlsGroup.AddTextfield("Show/Hide Log:", "Press L key", null);
                controlsGroup.AddTextfield("Show Statistics:", "Press R key", null);
                
                // システム情報表示
                group.AddSpace(10);
                var infoGroup = group.AddGroup("System Information");
                
                string frameworkVersion = System.Environment.Version.ToString();
                infoGroup.AddTextfield(".NET Framework:", frameworkVersion, null);
                
                string assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                infoGroup.AddTextfield("CS1Profiler Version:", assemblyVersion, null);
                
                UnityEngine.Debug.Log("[CS1Profiler] === OnSettingsUI COMPLETED ===");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] OnSettingsUI error: " + e.Message);
                
                // エラー時のフォールバック表示
                try
                {
                    var errorGroup = helper.AddGroup("CS1 Profiler - Error Mode");
                    errorGroup.AddTextfield("Status:", "Error occurred during UI setup", null);
                    errorGroup.AddTextfield("Controls:", "Use keyboard: P=toggle, L=log, R=stats", null);
                    errorGroup.AddTextfield("Error:", e.Message.Length > 60 ? e.Message.Substring(0, 60) + "..." : e.Message, null);
                }
                catch (Exception fallbackError)
                {
                    UnityEngine.Debug.LogError("[CS1Profiler] Fallback UI also failed: " + fallbackError.Message);
                }
            }
        }
    }
}
