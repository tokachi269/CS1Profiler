using System;
using System.Collections.Generic;
using UnityEngine;
using CS1Profiler.Managers;

namespace CS1Profiler
{
    public class PerformancePanel
    {
        private bool showPanel = false;
        private Rect panelRect = new Rect(10, 10, 400, 500);
        
        private PerformanceProfiler profiler;
        private Vector2 scrollPosition = Vector2.zero;
        
        // 表示オプション
        private bool showDetails = true;
        private bool showBottlenecks = true;

        // スタイル
        private GUIStyle headerStyle;
        private GUIStyle warningStyle;
        private GUIStyle criticalStyle;
        private GUIStyle normalStyle;
        private bool stylesInitialized = false;

        public PerformancePanel(PerformanceProfiler performanceProfiler)
        {
            profiler = performanceProfiler;
        }

        public void TogglePanel()
        {
            showPanel = !showPanel;
        }

        public bool IsVisible 
        { 
            get { return showPanel; } 
        }

        public void OnGUI()
        {
            if (!showPanel) return;

            InitializeStyles();

            // パネル背景
            GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
            panelRect = GUI.Window(12346, panelRect, DrawWindow, "CS1 Performance Profiler", GUI.skin.window);
            GUI.backgroundColor = Color.white;
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = Color.cyan;

            warningStyle = new GUIStyle(GUI.skin.label);
            warningStyle.normal.textColor = Color.yellow;
            warningStyle.fontStyle = FontStyle.Bold;

            criticalStyle = new GUIStyle(GUI.skin.label);
            criticalStyle.normal.textColor = Color.red;
            criticalStyle.fontStyle = FontStyle.Bold;

            normalStyle = new GUIStyle(GUI.skin.label);
            normalStyle.normal.textColor = Color.white;

            stylesInitialized = true;
        }

        private void DrawWindow(int windowID)
        {
            var data = profiler.CurrentData;

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            // ヘッダーボタン
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Details", showDetails ? GUI.skin.box : GUI.skin.button))
                showDetails = !showDetails;
            if (GUILayout.Button("Bottlenecks", showBottlenecks ? GUI.skin.box : GUI.skin.button))
                showBottlenecks = !showBottlenecks;
            if (GUILayout.Button("Close", GUI.skin.button))
                showPanel = false;
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // メインパフォーマンス指標
            GUILayout.Label("=== CURRENT PERFORMANCE ===", headerStyle);
            
            float currentFPS = data.FrameTime > 0 ? 1000f / data.FrameTime : 0f;
            var fpsStyle = currentFPS < profiler.FPSCriticalThreshold ? criticalStyle :
                          currentFPS < profiler.FPSWarningThreshold ? warningStyle : normalStyle;
            
            GUILayout.Label(string.Format("FPS: {0:F1} ({1:F1}ms)", currentFPS, data.FrameTime), fpsStyle);
            GUILayout.Label(string.Format("CPU: {0:F1}ms", data.CPUTime), normalStyle);
            
            var drawCallStyle = data.DrawCalls > profiler.DrawCallCriticalThreshold ? criticalStyle :
                               data.DrawCalls > profiler.DrawCallWarningThreshold ? warningStyle : normalStyle;
            GUILayout.Label(string.Format("Draw Calls: {0:N0} (Batches: {1}, SetPass: {2})", 
                data.DrawCalls, data.Batches, data.SetPassCalls), drawCallStyle);
            GUILayout.Label(string.Format("Triangles: {0:N0} (Vertices: {1:N0})", data.Triangles, data.Vertices), normalStyle);
            GUILayout.Label(string.Format("Memory: {0:N0}MB (GPU: {1:F1}MB)", 
                data.UsedMemory / (1024 * 1024), data.GPUMemoryMB), normalStyle);
            GUILayout.Label(string.Format("GPU: {0}", data.GPUName), normalStyle);

            GUILayout.Space(10);

            // 詳細情報
            if (showDetails)
            {
                GUILayout.Label("=== DETAILED BREAKDOWN ===", headerStyle);
                GUILayout.Label(string.Format("Simulation: {0:F1}ms", data.SimulationTime), normalStyle);
                GUILayout.Label(string.Format("Rendering: {0:F1}ms", data.RenderingTime), normalStyle);
                GUILayout.Label(string.Format("UI: {0:F1}ms", data.UITime), normalStyle);
                GUILayout.Label(string.Format("Vertices: {0:N0}", data.Vertices), normalStyle);
                GUILayout.Space(10);
            }

            // ボトルネック分析
            if (showBottlenecks)
            {
                GUILayout.Label("=== TOP BOTTLENECKS ===", headerStyle);
                var bottlenecks = profiler.GetTopBottlenecks(5);
                if (bottlenecks.Count > 0)
                {
                    foreach (var bottleneck in bottlenecks)
                    {
                        GUILayout.Label("• " + bottleneck, warningStyle);
                    }
                }
                else
                {
                    GUILayout.Label("No major bottlenecks detected", normalStyle);
                }
                GUILayout.Space(10);
            }

            // パフォーマンス推奨事項
            DrawRecommendations(data);

            GUILayout.EndScrollView();

            // ウィンドウドラッグ処理
            GUI.DragWindow();
        }

        private void DrawRecommendations(PerformanceProfiler.PerformanceData data)
        {
            var recommendations = new List<string>();

            if (data.DrawCalls > 5000)
            {
                recommendations.Add("Reduce draw calls by batching objects");
            }
            if (data.Triangles > 1000000)
            {
                recommendations.Add("Use LOD (Level of Detail) for distant objects");
            }
            if (data.UsedMemory > 3L * 1024 * 1024 * 1024) // 3GB
            {
                recommendations.Add("Clear unused assets and optimize textures");
            }
            if (data.SimulationTime > data.FrameTime * 0.5f)
            {
                recommendations.Add("Optimize simulation logic (citizen AI, traffic)");
            }
            if (data.RenderingTime > data.FrameTime * 0.6f)
            {
                recommendations.Add("Reduce graphics settings or camera draw distance");
            }

            if (recommendations.Count > 0)
            {
                GUILayout.Label("=== RECOMMENDATIONS ===", headerStyle);
                foreach (var rec in recommendations)
                {
                    GUILayout.Label("💡 " + rec, normalStyle);
                }
            }
        }
    }
}
