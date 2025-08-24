using UnityEngine;
using ICities;
using System;
using CS1Profiler.Managers;

namespace CS1Profiler.UI
{
    /// <summary>
    /// UIマネージャー - パネル表示とキー入力を管理
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private CS1Profiler.PerformancePanel performancePanel;
        private bool isUIEnabled = true;
        private float updateTimer = 0f;
        private const float UPDATE_INTERVAL = 1.0f;

        public static UIManager Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("[CS1Profiler] UIManager initialized");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            try
            {
                InitializeUI();
            }
            catch (Exception e)
            {
                Debug.LogError("[CS1Profiler] UIManager.Start error: " + e.Message);
            }
        }

        private void InitializeUI()
        {
            // パフォーマンスパネルを作成（手動復元版に対応）
            performancePanel = new CS1Profiler.PerformancePanel(null);
            Debug.Log("[CS1Profiler] PerformancePanel created successfully");
        }

        void Update()
        {
            try
            {
                HandleKeyInput();
                UpdateUI();
            }
            catch (Exception e)
            {
                Debug.LogError("[CS1Profiler] UIManager.Update error: " + e.Message);
            }
        }

        private void HandleKeyInput()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                TogglePanel();
            }
            
            if (Input.GetKeyDown(KeyCode.L))
            {
                RequestLog();
            }
            
            if (Input.GetKeyDown(KeyCode.R))
            {
                RequestStats();
            }
            
            if (Input.GetKeyDown(KeyCode.F12))
            {
                ExportStats();
            }
        }

        private void UpdateUI()
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= UPDATE_INTERVAL)
            {
                updateTimer = 0f;
                // UI更新は手動復元版のOnGUIで処理される
            }
        }

        public void TogglePanel()
        {
            if (performancePanel != null)
            {
                performancePanel.TogglePanel();
                Debug.Log("[CS1Profiler] Panel visibility toggled");
            }
        }

        public void RequestLog()
        {
            try
            {
                var profiler = CS1Profiler.Managers.ProfilerManager.Instance;
                if (profiler != null)
                {
                    profiler.LogCurrentStats();
                    Debug.Log("[CS1Profiler] Stats logged to console");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CS1Profiler] RequestLog error: " + e.Message);
            }
        }

        public void RequestStats()
        {
            try
            {
                var profiler = CS1Profiler.Managers.ProfilerManager.Instance;
                if (profiler != null)
                {
                    profiler.PrintDetailedStats();
                    Debug.Log("[CS1Profiler] Detailed stats printed");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CS1Profiler] RequestStats error: " + e.Message);
            }
        }

        public void ExportStats()
        {
            try
            {
                var profiler = CS1Profiler.Managers.ProfilerManager.Instance;
                if (profiler != null)
                {
                    profiler.ExportToCSV();
                    Debug.Log("[CS1Profiler] Stats exported to CSV");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CS1Profiler] ExportStats error: " + e.Message);
            }
        }

        public void SetUIEnabled(bool enabled)
        {
            isUIEnabled = enabled;
            Debug.Log($"[CS1Profiler] UI enabled: {enabled}");
        }

        void OnGUI()
        {
            if (isUIEnabled && performancePanel != null)
            {
                performancePanel.OnGUI();
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
