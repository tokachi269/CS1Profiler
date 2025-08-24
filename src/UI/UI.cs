// Cities: Skylines (CS1) 用：UI・キーボード操作管理

using System;
using ICities;
using UnityEngine;
using CS1Profiler.Managers;

namespace CS1Profiler
{
    /// <summary>
    /// 軽量なキーボード操作とログ管理
    /// </summary>
    public class Loading : LoadingExtensionBase
    {
        private static GameObject keyHandler;
        
        public override void OnLevelLoaded(LoadMode mode)
        {
            if (keyHandler == null)
            {
                keyHandler = new GameObject("CS1ProfilerKeys");
                keyHandler.AddComponent<KeyHandler>();
                UnityEngine.Debug.Log("[CS1Profiler] Keyboard handler loaded. Press P=toggle, L=log");
            }
        }

        public override void OnLevelUnloading()
        {
            if (keyHandler != null)
            {
                UnityEngine.Object.Destroy(keyHandler);
                keyHandler = null;
            }
        }
    }

    /// <summary>
    /// 最小限のキーボードハンドラー（TypeLoadException回避）
    /// </summary>
    public class KeyHandler : MonoBehaviour
    {
        private static bool showLog = false;
        private static string lastOutput = "";

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                UnityEngine.Debug.Log("[CS1Profiler] === P KEY PRESSED ===");
                try
                {
                    if (ProfilerManager.Instance != null)
                    {
                        ProfilerManager.Instance.ToggleProfiling();
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[CS1Profiler] ProfilerManager.Instance is null");
                    }
                    UnityEngine.Debug.Log("[CS1Profiler] === P KEY PROCESSING COMPLETED ===");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("[CS1Profiler] P key error: " + e.Message);
                }
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                UnityEngine.Debug.Log("[CS1Profiler] === L KEY PRESSED ===");
                showLog = !showLog;
                UnityEngine.Debug.Log("[CS1Profiler] Log display: " + (showLog ? "ON" : "OFF"));
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                UnityEngine.Debug.Log("[CS1Profiler] === R KEY PRESSED ===");
                try
                {
                    float fps = 1.0f / Time.deltaTime;
                    long mem = GC.GetTotalMemory(false) / 1024 / 1024;
                    UnityEngine.Debug.Log("[CS1Profiler] FPS:" + fps.ToString("F1") + " Memory:" + mem + "MB");

                    // リアルタイム統計をCSVに非同期記録
                    if (ProfilerManager.Instance != null && ProfilerManager.Instance.CsvManager != null)
                    {
                        ProfilerManager.Instance.CsvManager.QueueCsvWrite("Manual", "QuickStats", fps, 1, 0, 0, "FPS=" + fps.ToString("F1") + ",Memory=" + mem + "MB");
                    }
                    UnityEngine.Debug.Log("[CS1Profiler] === R KEY PROCESSING COMPLETED ===");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("[CS1Profiler] KeyHandler R key error: " + e.Message);
                }
            }
        }

        void OnGUI()
        {
            if (showLog && !string.IsNullOrEmpty(lastOutput))
            {
                // 背景とスタイル改善
                GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.fontSize = 11;
                boxStyle.normal.textColor = Color.white;
                boxStyle.normal.background = Texture2D.whiteTexture;
                boxStyle.alignment = TextAnchor.UpperLeft;

                GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
                GUI.Box(new Rect(10, 10, 650, 350), lastOutput, boxStyle);
                GUI.backgroundColor = Color.white;
            }

            // 右上にステータス表示（改善）
            GUIStyle statusStyle = new GUIStyle();
            statusStyle.fontSize = 14;
            bool profilingEnabled = ProfilerManager.Instance != null && ProfilerManager.Instance.IsProfilingEnabled();
            statusStyle.normal.textColor = profilingEnabled ? Color.green : Color.red;
            GUI.Label(new Rect(Screen.width - 200, 10, 190, 25),
                     "CS1Profiler: " + (profilingEnabled ? "ON" : "OFF"), statusStyle);

            // 操作ヘルプ
            GUIStyle helpStyle = new GUIStyle();
            helpStyle.fontSize = 10;
            helpStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(Screen.width - 150, 35, 140, 50),
                     "P: Toggle\nL: Log Display\nR: Stats", helpStyle);
        }

        public static void UpdateLog(string output)
        {
            lastOutput = output;
        }
    }
}
