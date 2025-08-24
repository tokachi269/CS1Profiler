using System;
using UnityEngine;
using CS1Profiler.Managers;

namespace CS1Profiler
{
    /// <summary>
    /// シンプルなパフォーマンス表示パネル
    /// </summary>
    public class PerformancePanel
    {
        private bool showPanel = false;
        private Rect panelRect = new Rect(10, 10, 400, 300);
        private Vector2 scrollPosition = Vector2.zero;

        // スタイル
        private GUIStyle headerStyle;
        private GUIStyle normalStyle;
        private bool stylesInitialized = false;

        public PerformancePanel(object unused)
        {
            // ProfilerManagerは不要 - 現在の実装では静的参照
        }

        public void TogglePanel()
        {
            showPanel = !showPanel;
        }

        public void OnGUI()
        {
            if (!showPanel) return;

            InitializeStyles();

            panelRect = GUI.Window(12345, panelRect, DrawPanel, "CS1 Profiler");
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            normalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            stylesInitialized = true;
        }

        private void DrawPanel(int windowID)
        {
            GUILayout.Space(5);

            // ヘッダー
            GUILayout.Label("Performance Monitor", headerStyle);
            GUILayout.Space(10);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            try
            {
                // 基本情報
                GUILayout.Label("=== System Info ===", headerStyle);
                
                float fps = 1.0f / Time.deltaTime;
                GUILayout.Label($"FPS: {fps:F1}", normalStyle);
                
                long memory = GC.GetTotalMemory(false) / 1024 / 1024;
                GUILayout.Label($"Memory: {memory} MB", normalStyle);
                
                GUILayout.Label($"Frame: {Time.frameCount}", normalStyle);
                
                GUILayout.Space(10);

                // プロファイリング状況
                GUILayout.Label("=== Profiling Status ===", headerStyle);
                
                bool isEnabled = CS1Profiler.Harmony.Hooks.IsEnabled();
                GUILayout.Label($"Profiling: {(isEnabled ? "Active" : "Inactive")}", normalStyle);
                
                var profiler = CS1Profiler.Managers.ProfilerManager.Instance;
                if (profiler != null)
                {
                    bool profilingEnabled = profiler.IsProfilingEnabled();
                    GUILayout.Label($"Data Collection: {(profilingEnabled ? "ON" : "OFF")}", normalStyle);
                    
                    string csvPath = profiler.GetCsvPath();
                    if (!string.IsNullOrEmpty(csvPath))
                    {
                        GUILayout.Label($"CSV: {System.IO.Path.GetFileName(csvPath)}", normalStyle);
                    }
                }

                GUILayout.Space(10);

                // コントロール
                GUILayout.Label("=== Controls ===", headerStyle);
                GUILayout.Label("P = Toggle Panel", normalStyle);
                GUILayout.Label("L = Log Stats", normalStyle);
                GUILayout.Label("R = Print Details", normalStyle);
                GUILayout.Label("F12 = Export CSV", normalStyle);

                GUILayout.Space(10);

                // 統計情報（簡略化）
                GUILayout.Label("=== Top Methods ===", headerStyle);
                try
                {
                    var stats = CS1Profiler.Profiling.PerformanceProfiler.GetTopMethods(5);
                    foreach (var stat in stats)
                    {
                        GUILayout.Label($"{stat.MethodName}: {stat.AverageMilliseconds:F3}ms", normalStyle);
                    }
                }
                catch (Exception e)
                {
                    GUILayout.Label($"Stats Error: {e.Message}", normalStyle);
                }
            }
            catch (Exception e)
            {
                GUILayout.Label($"Panel Error: {e.Message}", normalStyle);
            }

            GUILayout.EndScrollView();

            GUI.DragWindow();
        }
    }
}
