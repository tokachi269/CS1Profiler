using System;
using UnityEngine;

// 明示的に追加（同じ名前空間だが必要な場合）
using CS1Profiler.Managers;

namespace CS1Profiler.Managers
{
    /// <summary>
    /// プロファイラーの全体管理（MonoBehaviour対応）
    /// </summary>
    public class ProfilerManager : MonoBehaviour
    {
        private static ProfilerManager instance;
        public static ProfilerManager Instance => instance;
        
        private CSVManager csvManager;
        public CSVManager CsvManager => csvManager;
        private bool isProfilingEnabled = true;
        private bool isInitialized = false;
        private HarmonyLib.Harmony harmonyInstance;
        private int csvExportInterval = 60;
        private float lastAllCsvExportTime = 0f;
        
        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Initialize()
        {
            try
            {
                if (isInitialized) return;

                Debug.Log("[CS1Profiler] ProfilingManager initializing...");
                
                csvManager = new CSVManager();
                csvManager.Initialize();
                
                // Harmonyパッチ適用とログ抑制の初期化
                CS1Profiler.Patcher.PatchAll();


                if (CitiesHarmony.API.HarmonyHelper.IsHarmonyInstalled)
                {
                    harmonyInstance = new HarmonyLib.Harmony("CS1Profiler.MethodProfiler");
                    CS1Profiler.Profiling.MethodProfiler.Initialize(harmonyInstance);
                    isProfilingEnabled = true;
                    Debug.Log("[CS1Profiler] MethodProfiler initialized with Harmony");
                }
                else
                {
                    Debug.LogWarning("[CS1Profiler] Harmony is not installed, MethodProfiler disabled");
                }

                isInitialized = true;
                Debug.Log("[CS1Profiler] ProfilingManager initialized successfully");
            }
            catch (Exception e)
            {
                Debug.LogError("[CS1Profiler] ProfilingManager.Initialize error: " + e.Message);
            }
        }

        // UI/設定からの制御メソッド
        public void SetProfilingEnabled(bool enabled)
        {
            isProfilingEnabled = enabled;
            CS1Profiler.Profiling.MethodProfiler.SetEnabled(enabled);
        }

        public void SetCSVExportInterval(int seconds)
        {
            csvExportInterval = seconds;
        }

        void Update()
        {
            // 要件対応: F12キーでTop100をCSV出力
            if (Input.GetKeyDown(KeyCode.F12))
            {
                /*
                if (csvManager != null)
                {
                    csvManager.ExportTopN(100);
                }
                */
            }

            // 要件対応: All CSVを定期的に自動出力（30秒間隔）
            // 軽量化：処理が軽い生データ出力を使用
            if (Time.time - lastAllCsvExportTime > 30f)
            {
                /*
                if (csvManager != null)
                {
                    csvManager.ExportAllRawData(); // 軽量版を使用
                    lastAllCsvExportTime = Time.time;
                }
                */
            }
        }

        // 要件対応: Settings画面から呼べるメソッド
        public void ExportTop100FromSettings()
        {
            /*
            if (csvManager != null)
            {
                csvManager.ExportTopN(100);
            }
            */
        }

        public void ToggleProfiling()
        {
            SetProfilingEnabled(!isProfilingEnabled);
        }

        public void LogCurrentStats()
        {
            /*
            if (csvManager != null)
            {
                csvManager.LogCurrentStats();
            }
            */
        }

        public void PrintDetailedStats()
        {
            CS1Profiler.Profiling.MethodProfiler.PrintDetailedStats();
        }

        public void ExportToCSV()
        {
            /*
            if (csvManager != null)
            {
                csvManager.ExportToCSV();
            }
            */
        }

        public string GetCsvPath()
        {
            return ""; // csvManager?.GetCsvPath() ?? "";
        }

        public bool IsProfilingEnabled()
        {
            return isProfilingEnabled;
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                
                if (harmonyInstance != null)
                {
                    harmonyInstance.UnpatchAll("CS1Profiler.MethodProfiler");
                }
            }
        }
    }
}
