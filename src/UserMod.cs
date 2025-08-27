// Cities: Skylines (CS1) 用：メインMODクラス
// アーキテクチャ: CameraOperatorMod方式でインスタンス再生成対応

using ICities;
using System;
using System.Collections.Generic;
using UnityEngine;
using CS1Profiler.Managers;
using CS1Profiler.UI;

namespace CS1Profiler
{
    /// <summary>
    /// Cities Skylines MOD メインクラス - 統合プロファイリングシステム
    /// </summary>
    public class Mod : LoadingExtensionBase, IUserMod
    {
        // GameObject管理（CameraOperatorMod方式）
        private GameObject _profilerManagerObject;
        private const string ProfilerManagerName = "CS1ProfilerManager";
        
        // 性能分析システム
        private static PerformancePanel performancePanel;
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
            UnityEngine.Debug.Log("[CS1Profiler] === MOD OnEnabled (ProfilerManager Architecture) ===");
            
            // 起動時解析の開始
            if (_gameStartTime == DateTime.MinValue)
            {
                _gameStartTime = DateTime.Now;
                _startupProfilingActive = true;
                LogStartupEvent("MOD_ENABLED", "CS1Profiler mod enabled and startup analysis started");
            }
            
            // Harmonyパッチを初期化
            CS1Profiler.Patcher.PatchAll();
            
            InitializeProfilerManager();
            InitializePerformanceSystem();
            UnityEngine.Debug.Log("[CS1Profiler] === MOD OnEnabled COMPLETED ===");
        }

        // 性能分析システムの初期化
        private static void InitializePerformanceSystem()
        {
            try
            {
                if (!performanceSystemInitialized)
                {
                    UnityEngine.Debug.Log("[CS1Profiler] Initializing Performance Analysis System...");
                    
                    // パフォーマンスモニター用のGameObjectを作成
                    performanceMonitorObject = new GameObject("CS1ProfilerMonitor");
                    var manager = performanceMonitorObject.AddComponent<ProfilerManager>();
                    manager.Initialize();
                    
                    // パフォーマンスパネルを作成
                    performancePanel = new PerformancePanel(null);
                    
                    // UI管理システムも追加
                    performanceMonitorObject.AddComponent<UIManager>();
                    
                    UnityEngine.Object.DontDestroyOnLoad(performanceMonitorObject);
                    
                    performanceSystemInitialized = true;
                    UnityEngine.Debug.Log("[CS1Profiler] Performance Analysis System initialized successfully");
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Failed to initialize Performance System: " + e.Message);
            }
        }

        public void OnGUI()
        {
            // キー入力処理
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.P)
                {
                    if (performancePanel != null)
                    {
                        performancePanel.TogglePanel();
                    }
                }
                else if (Event.current.keyCode == KeyCode.F12)
                {
                    // CSV出力
                    var profilerManager = ProfilerManager.Instance;
                    if (profilerManager != null)
                    {
                        profilerManager.ExportToCSV();
                        UnityEngine.Debug.Log("[CS1Profiler] Manual CSV export triggered");
                    }
                }
            }
            
            // PerformancePanelを直接描画
            if (performancePanel != null)
            {
                performancePanel.OnGUI();
            }
        }

        public void Update()
        {
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
                    UnityEngine.Debug.Log($"[CS1Profiler] CSV output completed at {_lastCsvOutput:HH:mm:ss}");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[CS1Profiler] CSV output error: {e.Message}");
                }
            }
        }

        public void OnDisabled()
        {
            UnityEngine.Debug.Log("[CS1Profiler] === MOD OnDisabled ===");
            
            // Harmonyパッチをクリーンアップ
            CS1Profiler.Patcher.UnpatchAll();
            
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

                // ProfilerManagerを作成
                _profilerManagerObject = new GameObject(ProfilerManagerName);
                _profilerManagerObject.AddComponent<ProfilerManager>();
                UnityEngine.Object.DontDestroyOnLoad(_profilerManagerObject);
                
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
                
                // ProfilerManagerはstaticなので特にクリーンアップ不要
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
            UnityEngine.Debug.Log($"[CS1Profiler] === OnLevelLoaded: Map Loaded (Mode: {mode}) ===");
            
            // ゲーム状態をInGameに変更
            NotifyGameStateChanged("InGame");
            
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
            UnityEngine.Debug.Log("[CS1Profiler] === OnCreated: Loading Started ===");
            
            // ゲーム状態をLoadingに変更
            NotifyGameStateChanged("Loading");
        }

        public override void OnReleased()
        {
            LogStartupEvent("LOADING_RELEASED", "Loading manager released - cleanup initiated");
            UnityEngine.Debug.Log("[CS1Profiler] === OnReleased: Back to Menu ===");
            
            // ゲーム状態をMainMenuに変更
            NotifyGameStateChanged("MainMenu");
            
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
                    UnityEngine.Debug.Log($"[CS1Profiler] Logging startup event: {eventType} - Memory: {memoryMB}MB");
                    ProfilerManager.Instance.ExportToCSV();
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
                if (ProfilerManager.Instance != null)
                {
                    ProfilerManager.Instance.ExportToCSV();
                    UnityEngine.Debug.Log("[CS1Profiler] Startup analysis CSV exported");
                }
                
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] GenerateStartupReport error: " + e.Message);
            }
        }
        
        // オプション画面（修正版）
        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] OnSettingsUI starting...");
                
                // メイングループを作成
                var mainGroup = helper.AddGroup("Profiler");
                
                // ステータス情報
                mainGroup.AddSpace(5);
                
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
                
                mainGroup.AddTextfield("Profiling Status:", profilingStatus, null);
                mainGroup.AddTextfield("CSV Output:", csvPath, null);
                mainGroup.AddTextfield("Current Time:", DateTime.Now.ToString("HH:mm:ss"), null);
                
                // プロファイリング制御設定
                mainGroup.AddSpace(10);
                try
                {
                    bool currentProfilingState = false;
                    if (ProfilerManager.Instance != null)
                    {
                        currentProfilingState = ProfilerManager.Instance.IsProfilingEnabled();
                    }
                    
                    mainGroup.AddCheckbox("Enable Performance Profiling:", 
                        currentProfilingState, 
                        (value) => {
                            try
                            {
                                if (ProfilerManager.Instance != null)
                                {
                                    // 現在の状態と設定値が異なる場合のみトグル
                                    bool currentState = ProfilerManager.Instance.IsProfilingEnabled();
                                    if (currentState != value)
                                    {
                                        ProfilerManager.Instance.SetProfilingEnabled(value);
                                        UnityEngine.Debug.Log("[CS1Profiler] Profiling set from options: " + (value ? "ENABLED" : "DISABLED"));
                                        
                                        // 設定変更をユーザーに通知
                                        if (value)
                                        {
                                            UnityEngine.Debug.Log("[CS1Profiler] Performance monitoring and CSV recording started");
                                        }
                                        else
                                        {
                                            UnityEngine.Debug.Log("[CS1Profiler] Performance monitoring and CSV recording stopped");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                UnityEngine.Debug.LogError("[CS1Profiler] Error toggling profiling: " + ex.Message);
                            }
                        });
                }
                catch (Exception ex)
                {
                    mainGroup.AddTextfield("Profiling Control:", "Error: " + ex.Message, null);
                }
                
                // ログ抑制設定
                mainGroup.AddSpace(5);
                mainGroup.AddCheckbox("Suppress PackageDeserializer Warnings:", 
                    LogSuppressionHooks.SuppressPackageDeserializerLogs, 
                    (value) => {
                        LogSuppressionHooks.SuppressPackageDeserializerLogs = value;
                        UnityEngine.Debug.Log("[CS1Profiler] PackageDeserializer log suppression: " + (value ? "ENABLED" : "DISABLED"));
                    });

                // コントロール説明
                mainGroup.AddSpace(10);
                mainGroup.AddTextfield("Toggle Profiling:", "Press P key in-game", null);
                mainGroup.AddTextfield("Show/Hide Log:", "Press L key in-game", null);  
                mainGroup.AddTextfield("Reset Statistics:", "Press R key in-game", null);
                
                // インスタンス管理機能
                mainGroup.AddSpace(10);
                mainGroup.AddButton("🔄 Instance Reset + Show Performance Panel", () => {
                    try
                    {
                        UnityEngine.Debug.Log("[CS1Profiler] === INSTANCE RESET + PERFORMANCE PANEL REQUESTED ===");
                        
                        // インスタンスリセットコールバックを設定
                        InstanceManager.SetRecreateCallback(() => {
                            // オブジェクト参照をクリア（破棄済みのため）
                            _profilerManagerObject = null;
                            performanceMonitorObject = null;
                            performanceSystemInitialized = false;
                            
                            // ProfilerManagerとPerformanceSystemを再作成
                            InitializeProfilerManager();
                            InitializePerformanceSystem();
                            
                            UnityEngine.Debug.Log("[CS1Profiler] Instance reset completed");
                        });
                        
                        // インスタンスリセット実行
                        InstanceManager.ResetAllInstances();
                        
                        UnityEngine.Debug.Log("[CS1Profiler] === INSTANCE RESET + PERFORMANCE PANEL COMPLETED ===");
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError("[CS1Profiler] Instance Reset + Performance Panel failed: " + ex.ToString());
                    }
                });
                
                // パフォーマンスパネル専用ボタン
                mainGroup.AddButton("📊 Toggle Performance Panel", () => {
                    try
                    {
                        InitializePerformanceSystem();
                        if (performancePanel != null)
                        {
                            performancePanel.TogglePanel();
                            UnityEngine.Debug.Log("[CS1Profiler] Performance Panel toggled via UI");
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("[CS1Profiler] Performance Panel not initialized");
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError("[CS1Profiler] Toggle Performance Panel failed: " + ex.ToString());
                    }
                });
                
                mainGroup.AddTextfield("Instance Reset:", "Click button above to reset + show performance panel", null);
                mainGroup.AddTextfield("Performance Panel:", "Press P key or use button above", null);
                
                // クリップボード機能
                mainGroup.AddSpace(10);
                mainGroup.AddButton("📋 Copy Patched Methods List", () => {
                    try
                    {
                        string patchedMethods = GetPatchedMethodsList();
                        GUIUtility.systemCopyBuffer = patchedMethods;
                        UnityEngine.Debug.Log("[CS1Profiler] Patched methods list copied to clipboard");
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError("[CS1Profiler] Copy patched methods failed: " + ex.Message);
                    }
                });
                
                mainGroup.AddButton("📋 Copy Mods List", () => {
                    try
                    {
                        string modsList = GetModsList();
                        GUIUtility.systemCopyBuffer = modsList;
                        UnityEngine.Debug.Log("[CS1Profiler] MODs list copied to clipboard");
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError("[CS1Profiler] Copy MODs list failed: " + ex.Message);
                    }
                });
                
                // システム情報
                mainGroup.AddSpace(10);
                mainGroup.AddTextfield("MOD Version:", "1.0.0", null);
                mainGroup.AddTextfield("Framework:", System.Environment.Version.ToString(), null);
                
                UnityEngine.Debug.Log("[CS1Profiler] OnSettingsUI completed successfully");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] OnSettingsUI failed: " + e.ToString());
                
                // フォールバック
                try
                {
                    var fallbackGroup = helper.AddGroup("CS1 Profiler [Error]");
                    fallbackGroup.AddTextfield("Status:", "Configuration error occurred", null);
                    fallbackGroup.AddTextfield("Solution:", "Check game logs for details", null);
                }
                catch
                {
                    UnityEngine.Debug.LogError("[CS1Profiler] Complete UI failure");
                }
            }
        }
        
        // ===============================================
        // 状態管理メソッド
        // ===============================================
        
        /// <summary>
        /// ゲーム状態変更をCSVManagerに通知
        /// </summary>
        private static void NotifyGameStateChanged(string newState)
        {
            try
            {
                var profilerManager = ProfilerManager.Instance;
                if (profilerManager?.CsvManager != null)
                {
                    // プロファイリングが無効な場合は状態変更も通知しない
                    if (!profilerManager.IsProfilingEnabled()) return;
                    
                    UnityEngine.Debug.Log($"[CS1Profiler] Game state changed to: {newState}");
                    
                    // CSVManagerの状態切り替えメソッドを呼び出し
                    // SwitchGameState メソッドを CSVManager に追加する
                    var csvManager = profilerManager.CsvManager;
                    if (csvManager != null)
                    {
                        // リフレクションまたは新メソッドで状態変更を通知
                        csvManager.SwitchGameState(newState);
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[CS1Profiler] ProfilerManager or CSVManager not available for state change");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] Error notifying game state change: {e.Message}");
            }
        }
        
        /// <summary>
        /// パッチ済みメソッドのリストを取得
        /// </summary>
        private string GetPatchedMethodsList()
        {
            try
            {
                return CS1Profiler.PerformancePatches.GetPatchedMethodsList();
            }
            catch (Exception e)
            {
                return $"Error retrieving patched methods: {e.Message}";
            }
        }
        
        /// <summary>
        /// 検出されたMODのリストを取得
        /// </summary>
        private string GetModsList()
        {
            try
            {
                return CS1Profiler.PerformancePatches.GetModsList();
            }
            catch (Exception e)
            {
                return $"Error retrieving MOD list: {e.Message}";
            }
        }
    }
}
