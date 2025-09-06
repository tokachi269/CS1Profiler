using ICities;
using System;
using System.Collections.Generic;
using UnityEngine;
using CS1Profiler.Managers;
using CS1Profiler.Harmony;
using CS1Profiler.Core;

namespace CS1Profiler
{
    /// <summary>
    /// Cities Skylines MOD メインクラス - 統合プロファイリングシステム
    /// </summary>
    public class Mod : LoadingExtensionBase, IUserMod
    {
        // GameObject管理
        private GameObject _profilerManagerObject;
        private const string ProfilerManagerName = Constants.PROFILER_MANAGER_NAME;

        // 性能分析システム
        // private static PerformancePanel performancePanel; // TODO: 実装予定
        private static GameObject performanceMonitorObject;
        private static bool performanceSystemInitialized = false;
        

        
        // 起動時解析用の追加フィールド
        private static DateTime _gameStartTime = DateTime.MinValue;
        private static bool _startupProfilingActive = false;
        private static readonly List<string> _startupLog = new List<string>();
        
        // CSV出力タイマー
        private static DateTime _lastCsvOutput = DateTime.MinValue;
        private const int CSV_OUTPUT_INTERVAL_SECONDS = 30; // 30秒間隔でCSV出力
        private static bool _csvAutoOutputEnabled = true; // CSV自動出力のデフォルトはON

        // 5分間の一時的プロファイリング機能
        private static DateTime _performanceProfilingStartTime = DateTime.MinValue;
        private static DateTime _simulationProfilingStartTime = DateTime.MinValue;
        private const int PROFILING_DURATION_MINUTES = 5; // 5分間の自動停止
        private static bool _performanceProfilingTimerActive = false;
        private static bool _simulationProfilingTimerActive = false;

        // IUserMod必須プロパティ
        public string Name 
        { 
            get { return "Performance Analyzer"; } 
        }
        
        public string Description 
        { 
            get { return "Detailed startup and loading performance profiler for Cities: Skylines"; } 
        }

        public void OnEnabled()
        {
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} === MOD OnEnabled (ProfilerManager Architecture) ===");
            
            // 起動時解析の開始
            if (_gameStartTime == DateTime.MinValue)
            {
                _gameStartTime = DateTime.Now;
                _startupProfilingActive = true;
                LogStartupEvent("MOD_ENABLED", $"{Constants.MOD_NAME} mod enabled and startup analysis started");
            }
            
            // 統一パッチ管理システムを初期化
            CS1Profiler.Harmony.PatchController.Initialize();
            
            InitializeProfilerManager();
            InitializePerformanceSystem();
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} === MOD OnEnabled COMPLETED ===");
        }

        // 性能分析システムの初期化
        private static void InitializePerformanceSystem()
        {
            try
            {
                if (!performanceSystemInitialized)
                {
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Initializing Performance Analysis System...");
                    
                    // パフォーマンスモニター用のGameObjectを作成
                    performanceMonitorObject = new GameObject(Constants.PERFORMANCE_MONITOR_NAME);
                    var manager = performanceMonitorObject.AddComponent<ProfilerManager>();
                    manager.Initialize();
                    
                    // パフォーマンスパネルを作成
                    // performancePanel = new PerformancePanel(null); // TODO: 実装予定
                    
                    // UI管理システムも追加
                    // performanceMonitorObject.AddComponent<UIManager>(); // TODO: 実装予定
                    
                    UnityEngine.Object.DontDestroyOnLoad(performanceMonitorObject);
                    
                    performanceSystemInitialized = true;
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance Analysis System initialized successfully");
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to initialize Performance System: " + e.Message);
            }
        }

        public void OnGUI()
        {
            // キー入力処理
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.P)
                {
                    // if (performancePanel != null) // TODO: 実装予定
                    // {
                    //     performancePanel.TogglePanel();
                    // }
                }
                else if (Event.current.keyCode == KeyCode.F12)
                {
                    // CSV出力
                    var profilerManager = ProfilerManager.Instance;
                    if (profilerManager != null)
                    {
                        profilerManager.ExportToCSV();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Manual CSV export triggered");
                    }
                }
                // 他のキーは削除（ショートカット不要）
            }
            
            // PerformancePanelを直接描画
            // if (performancePanel != null) // TODO: 実装予定
            // {
            //     performancePanel.OnGUI();
            // }
        }

        public void Update()
        {
            // Performance Profiling 5分タイマーチェック
            if (_performanceProfilingTimerActive && _performanceProfilingStartTime != DateTime.MinValue)
            {
                var elapsed = DateTime.Now - _performanceProfilingStartTime;
                if (elapsed.TotalMinutes >= PROFILING_DURATION_MINUTES)
                {
                    // 自動停止
                    PatchController.PerformanceProfilingEnabled = false;
                    _performanceProfilingTimerActive = false;
                    _performanceProfilingStartTime = DateTime.MinValue;
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance Profiling automatically stopped after 5 minutes");
                }
            }

            // Simulation Step Profiling 5分タイマーチェック
            if (_simulationProfilingTimerActive && _simulationProfilingStartTime != DateTime.MinValue)
            {
                var elapsed = DateTime.Now - _simulationProfilingStartTime;
                if (elapsed.TotalMinutes >= PROFILING_DURATION_MINUTES)
                {
                    // 自動停止
                    PatchController.SimulationProfilingEnabled = false;
                    _simulationProfilingTimerActive = false;
                    _simulationProfilingStartTime = DateTime.MinValue;
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Simulation Step Profiling automatically stopped after 5 minutes");
                }
            }

            // 両方停止時の完了ログ
            if (!_performanceProfilingTimerActive && !_simulationProfilingTimerActive && 
                DateTime.Now.Second % 30 == 0) // 30秒ごとに1回だけチェック
            {
                // 完了状態の確認（ログスパム防止）
            }

            // 定期的なCSV出力（30秒間隔）- 自動出力が有効な場合のみ
            if (_csvAutoOutputEnabled && 
                (_lastCsvOutput == DateTime.MinValue || 
                (DateTime.Now - _lastCsvOutput).TotalSeconds >= CSV_OUTPUT_INTERVAL_SECONDS))
            {
                try
                {
                    // ProfilerManagerのCSV出力を呼び出し
                    var profilerManager = ProfilerManager.Instance;
                    if (profilerManager != null)
                    {
                        profilerManager.ExportToCSV();
                    }
                    
                    _lastCsvOutput = DateTime.Now;
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} CSV output completed at {_lastCsvOutput:HH:mm:ss}");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} CSV output error: {e.Message}");
                }
            }
        }

        public void OnDisabled()
        {
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} === MOD OnDisabled ===");
            
            // 統一パッチ管理システムをシャットダウン
            CS1Profiler.Harmony.PatchController.Shutdown();
            
            DestroyProfilerManager();
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} === MOD OnDisabled COMPLETED ===");
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
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Found existing ProfilerManager, destroying...");
                    UnityEngine.Object.Destroy(existingManager);
                }

                // ProfilerManagerを作成
                _profilerManagerObject = new GameObject(ProfilerManagerName);
                _profilerManagerObject.AddComponent<ProfilerManager>();
                UnityEngine.Object.DontDestroyOnLoad(_profilerManagerObject);
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} ProfilerManager initialized successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} ProfilerManager initialization failed: " + e.Message);
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Stack trace: " + e.StackTrace);
            }
        }

        // ProfilerManager破棄
        private void DestroyProfilerManager()
        {
            try
            {
                if (_profilerManagerObject != null)
                {
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Destroying ProfilerManager...");
                    UnityEngine.Object.Destroy(_profilerManagerObject);
                    _profilerManagerObject = null;
                }
                
                // ProfilerManagerはstaticなので特にクリーンアップ不要
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} ProfilerManager destroyed successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} ProfilerManager destruction error: " + e.Message);
            }
        }

        // LoadingExtensionBase オーバーライド（起動解析機能付き）
        public override void OnLevelLoaded(LoadMode mode)
        {
            LogStartupEvent("LEVEL_LOADED", "Level loaded - Mode: " + mode.ToString());
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} OnLevelLoaded - Mode: " + mode);
            
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
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} OnLevelUnloading");
        }

        public override void OnCreated(ILoading loading)
        {
            LogStartupEvent("LOADING_CREATED", "Loading manager created");
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} OnCreated");
        }

        public override void OnReleased()
        {
            LogStartupEvent("LOADING_RELEASED", "Loading manager released - cleanup initiated");
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} OnReleased - Complete cleanup");
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
                if (ProfilerManager.Instance != null)
                {
                    long memoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Logging startup event: {eventType} - Memory: {memoryMB}MB");
                    ProfilerManager.Instance.ExportToCSV();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} LogStartupEvent error: " + e.Message);
            }
        }

        // 起動解析レポート生成
        private static void GenerateStartupReport()
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} === STARTUP ANALYSIS REPORT ===");
                
                DateTime endTime = DateTime.Now;
                TimeSpan totalStartupTime = endTime - _gameStartTime;
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Total startup time: " + totalStartupTime.TotalSeconds.ToString("F2") + " seconds");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Startup events captured: " + _startupLog.Count);
                
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
                if (ProfilerManager.Instance != null)
                {
                    ProfilerManager.Instance.ExportToCSV();
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Startup analysis CSV exported");
                }
                
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} GenerateStartupReport error: " + e.Message);
            }
        }
        
        // オプション画面（修正版）
        /// <summary>
        /// パッチの現在状態を取得
        /// </summary>
        /// <summary>
        /// 読み込み済みMOD一覧を取得（有効なMODのみ、ID 名前形式）
        /// </summary>
        private static string GetLoadedModsList()
        {
            var modList = new System.Text.StringBuilder();
            
            try
            {
                var pluginManager = ColossalFramework.Plugins.PluginManager.instance;
                int enabledCount = 0;
                
                foreach (var plugin in pluginManager.GetPluginsInfo())
                {
                    if (!plugin.isBuiltin && plugin.isEnabled) // 有効なMODのみ
                    {
                        // WorkshopIDを取得（SteamワークショップアイテムID）
                        string modId = "local";
                        if (plugin.publishedFileID != null && plugin.publishedFileID.AsUInt64 > 0)
                        {
                            modId = plugin.publishedFileID.AsUInt64.ToString();
                        }
                        
                        // MOD名を取得（複数の方法を試行）
                        string modName = plugin.name;
                        if (string.IsNullOrEmpty(modName))
                        {
                            // plugin.nameが空の場合、アセンブリ名から推測
                            var assemblies = plugin.GetAssemblies();
                            if (assemblies != null && assemblies.Count > 0)
                            {
                                modName = assemblies[0].GetName().Name;
                            }
                        }
                        if (string.IsNullOrEmpty(modName))
                        {
                            // 最後の手段として"Unknown"を使用
                            modName = "Unknown";
                        }
                        
                        modList.AppendLine($"{modId} {modName}");
                        enabledCount++;
                    }
                }
            }
            catch (Exception e)
            {
                modList.AppendLine($"Error: {e.Message}");
            }
            
            return modList.ToString();
        }

        /// <summary>
        /// MOD一覧をクリップボードにコピー
        /// </summary>
        private static void CopyModListToClipboard()
        {
            try
            {
                string modList = GetLoadedModsList();
                GUIUtility.systemCopyBuffer = modList;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} MOD一覧をクリップボードにコピーしました");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} MOD一覧のコピーに失敗: {e.Message}");
            }
        }

        private string GetPatchStatus()
        {
            var mode = CS1Profiler.Harmony.PatchController.IsLightweightMode ? "Lightweight" : "Analysis";
            return $"Mode: {mode}";
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} OnSettingsUI starting...");
                
                // グループ1: MOD最適化（ゲームプレイヤーが常に使うもの）
                var optimizationGroup = helper.AddGroup("MOD Optimizations");
                
                optimizationGroup.AddCheckbox("RenderIt Optimization:", 
                    CS1Profiler.Harmony.PatchController.RenderItOptimizationEnabled, 
                    (value) => {
                        CS1Profiler.Harmony.PatchController.RenderItOptimizationEnabled = value;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} RenderIt optimization: " + (value ? "ENABLED" : "DISABLED"));
                    });

                optimizationGroup.AddCheckbox("PloppableAsphaltFix Optimization:", 
                    CS1Profiler.Harmony.PatchController.PloppableAsphaltFixOptimizationEnabled, 
                    (value) => {
                        CS1Profiler.Harmony.PatchController.PloppableAsphaltFixOptimizationEnabled = value;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix optimization: " + (value ? "ENABLED" : "DISABLED"));
                    });

                // グループ2: 性能分析とシステム情報
                var analysisGroup = helper.AddGroup("Performance Analysis & System");
                analysisGroup.AddCheckbox("Suppress PackageDeserializer Warnings:", 
                    CS1Profiler.Harmony.LogSuppressionHooks.SuppressPackageDeserializerLogs, 
                    (value) => {
                        CS1Profiler.Harmony.LogSuppressionHooks.SuppressPackageDeserializerLogs = value;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PackageDeserializer log suppression: " + (value ? "ENABLED" : "DISABLED"));
                    });
                // ステータス情報
                string profilingStatus = "STOPPED";
                string csvPath = "Not available";
                
                try
                {
                    if (ProfilerManager.Instance != null)
                    {
                        profilingStatus = ProfilerManager.Instance.IsProfilingEnabled() ? "ACTIVE" : "STOPPED";
                        csvPath = ProfilerManager.Instance.GetCsvPath();
                        if (!string.IsNullOrEmpty(csvPath) && csvPath.Length > 50)
                        {
                            csvPath = "..." + csvPath.Substring(csvPath.Length - 47);
                        }
                    }
                }
                catch
                {
                    profilingStatus = "ERROR";
                    csvPath = "Unable to get path";
                }
                
                analysisGroup.AddTextfield("Profiling Status:", profilingStatus, null);
                analysisGroup.AddTextfield("CSV Output:", csvPath, null);
                
                // Performance & Simulation Profiling - 5分間の一時計測ボタン（統合版）
                bool anyProfilingActive = _performanceProfilingTimerActive || _simulationProfilingTimerActive;
                
                // 分析制御ボタン（常に表示）
                analysisGroup.AddButton("Start 5-min Analysis", () => {
                    try
                    {
                        // Performance Profiling開始（パッチ適用）
                        CS1Profiler.Harmony.PatchController.PerformanceProfilingEnabled = true;
                        _performanceProfilingStartTime = DateTime.Now;
                        _performanceProfilingTimerActive = true;
                        
                        // Simulation Profiling開始（パッチ適用）
                        CS1Profiler.Harmony.PatchController.SimulationProfilingEnabled = true;
                        _simulationProfilingStartTime = DateTime.Now;
                        _simulationProfilingTimerActive = true;
                        
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Complete analysis started (Performance + Simulation, 5-minute timer)");
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Waiting 10 seconds before starting CSV output (patch stabilization)");
                        
                        // 10秒後にCSV出力を開始するコルーチンを開始
                        if (ProfilerManager.Instance != null)
                        {
                            ProfilerManager.Instance.StartCoroutine(StartCSVOutputAfterDelay());
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to start analysis: " + ex.Message);
                        // エラー時はタイマーをリセット
                        _performanceProfilingTimerActive = false;
                        _simulationProfilingTimerActive = false;
                        _performanceProfilingStartTime = DateTime.MinValue;
                        _simulationProfilingStartTime = DateTime.MinValue;
                    }
                });
                
                analysisGroup.AddButton("⏹️ Stop Analysis", () => {
                    // Performance Profiling停止
                    if (_performanceProfilingTimerActive)
                    {
                        CS1Profiler.Harmony.PatchController.PerformanceProfilingEnabled = false;
                        _performanceProfilingTimerActive = false;
                        _performanceProfilingStartTime = DateTime.MinValue;
                    }
                    
                    // Simulation Profiling停止
                    if (_simulationProfilingTimerActive)
                    {
                        CS1Profiler.Harmony.PatchController.SimulationProfilingEnabled = false;
                        _simulationProfilingTimerActive = false;
                        _simulationProfilingStartTime = DateTime.MinValue;
                    }
                    
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Complete analysis stopped manually (Performance + Simulation)");
                });
                
                // System Information
                analysisGroup.AddTextfield("Current Status:", GetPatchStatus(), null);
                
                // システム情報
                analysisGroup.AddSpace(10);
                analysisGroup.AddTextfield("MOD Version:", "1.0.0", null);
                analysisGroup.AddTextfield("Framework:", System.Environment.Version.ToString(), null);
                
                // MOD一覧機能
                analysisGroup.AddSpace(5);
                analysisGroup.AddButton("📋 Copy MOD List to Clipboard", () => {
                    CopyModListToClipboard();
                });
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} OnSettingsUI completed successfully");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} OnSettingsUI failed: " + e.ToString());
                
                // フォールバック
                try
                {
                    var fallbackGroup = helper.AddGroup("CS1 Profiler [Error]");
                    fallbackGroup.AddTextfield("Status:", "Configuration error occurred", null);
                    fallbackGroup.AddTextfield("Solution:", "Check game logs for details", null);
                }
                catch
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Complete UI failure");
                }
            }
        }
        
        /// <summary>
        /// 10秒待機後にCSV出力を開始するコルーチン
        /// パッチ適用直後の重い処理を避けるため
        /// </summary>
        private System.Collections.IEnumerator StartCSVOutputAfterDelay()
        {
            // 10秒待機
            yield return new UnityEngine.WaitForSeconds(10.0f);
            
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} 10-second stabilization period completed, starting CSV output");
            
            // ProfilerManagerの実際のCSV出力を開始
            try
            {
                if (ProfilerManager.Instance != null && ProfilerManager.Instance.CsvManager != null)
                {
                    // CSVマネージャーを有効化
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} CSV output system activated");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} ProfilerManager or CSVManager not available for delayed start");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to start delayed CSV output: " + ex.Message);
            }
        }
    }
}
