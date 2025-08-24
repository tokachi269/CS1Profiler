using UnityEngine;
using CS1Profiler.Managers;

namespace CS1Profiler
{
    /// <summary>
    /// 性能分析システムのMonoBehaviour管理クラス
    /// Update/OnGUIを処理するため
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        private static PerformanceProfiler performanceProfiler;
        private static PerformancePanel performancePanel;
        
        public static void Initialize(PerformanceProfiler profiler, PerformancePanel panel)
        {
            performanceProfiler = profiler;
            performancePanel = panel;
        }
        
        void Update()
        {
            try
            {
                if (performanceProfiler != null)
                {
                    performanceProfiler.UpdatePerformanceData();
                }
                
                // Pキーでパネルをトグル
                if (Input.GetKeyDown(KeyCode.P))
                {
                    if (performancePanel != null)
                    {
                        performancePanel.TogglePanel();
                        Debug.Log("[CS1Profiler] Performance Panel toggled with P key");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[CS1Profiler] PerformanceMonitor Update error: " + e.Message);
            }
        }
        
        void OnGUI()
        {
            try
            {
                if (performancePanel != null)
                {
                    performancePanel.OnGUI();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[CS1Profiler] PerformanceMonitor OnGUI error: " + e.Message);
            }
        }
        
        void OnDestroy()
        {
            Debug.Log("[CS1Profiler] PerformanceMonitor destroyed");
        }
    }
}
