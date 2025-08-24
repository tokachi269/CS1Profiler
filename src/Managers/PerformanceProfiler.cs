using System;
using System.Collections.Generic;
using UnityEngine;

namespace CS1Profiler.Managers
{
    public class PerformanceProfiler
    {
        // 性能データ構造体（.NET 3.5互換）
        public struct PerformanceData
        {
            public float FrameTime;
            public float CPUTime;
            public int DrawCalls;
            public int Triangles;
            public int Vertices;
            public long TotalMemory;
            public long UsedMemory;
            public float GPUMemoryMB;
            public string GPUName;
            public int SetPassCalls;
            public int Batches;
            public DateTime Timestamp;
            public int FrameCount;
            public float SimulationTime;
            public float RenderingTime;
            public float UITime;
        }

        // 履歴データ
        private List<PerformanceData> performanceHistory = new List<PerformanceData>();
        private const int MaxHistorySize = 300; // 5分間分（60FPS基準）

        // CSV出力用カウンター
        private int csvOutputCounter = 0;
        private const int CsvOutputInterval = 60; // 1秒間に1回出力（60FPS基準）

        // プロファイリング用タイマー（.NET 3.5互換）
        private DateTime simulationStartTime;
        private DateTime renderingStartTime;
        private DateTime uiStartTime;
        private bool isSimulationRunning = false;
        private bool isRenderingRunning = false;
        private bool isUIRunning = false;

        // 最新データ
        public PerformanceData CurrentData { get; private set; }
        public PerformanceData PreviousData { get; private set; }

        // アラート設定
        public float FPSWarningThreshold = 30f;
        public float FPSCriticalThreshold = 20f;
        public int DrawCallWarningThreshold = 5000;
        public int DrawCallCriticalThreshold = 10000;
        public long MemoryWarningThreshold = 4L * 1024 * 1024 * 1024; // 4GB

        public void StartFrame()
        {
            simulationStartTime = DateTime.Now;
            isSimulationRunning = true;
        }

        public void EndSimulation()
        {
            if (isSimulationRunning)
            {
                isSimulationRunning = false;
                renderingStartTime = DateTime.Now;
                isRenderingRunning = true;
            }
        }

        public void EndRendering()
        {
            if (isRenderingRunning)
            {
                isRenderingRunning = false;
                uiStartTime = DateTime.Now;
                isUIRunning = true;
            }
        }

        public void EndUI()
        {
            if (isUIRunning)
            {
                isUIRunning = false;
            }
        }

        public void UpdatePerformanceData()
        {
            PreviousData = CurrentData;

            var now = DateTime.Now;

            var newData = new PerformanceData
            {
                Timestamp = now,
                FrameCount = Time.frameCount,
                FrameTime = Time.deltaTime * 1000f, // ms
                CPUTime = Time.deltaTime * 1000f,
                DrawCalls = 0, // Unity 5.6では取得不可
                Triangles = 0, // Unity 5.6では取得不可
                Vertices = 0, // Unity 5.6では取得不可
                TotalMemory = GetTotalMemory(),
                UsedMemory = GetUsedMemory(),
                GPUMemoryMB = GetGPUMemory(),
                GPUName = GetGPUName(),
                SetPassCalls = 0, // Unity 5.6では取得不可
                Batches = 0, // Unity 5.6では取得不可
                SimulationTime = 0f,
                RenderingTime = 0f,
                UITime = 0f
            };

            CurrentData = newData;

            // 履歴に追加
            performanceHistory.Add(newData);
            if (performanceHistory.Count > MaxHistorySize)
            {
                performanceHistory.RemoveAt(0);
            }

            // CSV出力（5秒間に1回メソッドプロファイル結果を出力）
            csvOutputCounter++;
            if (csvOutputCounter >= 300) // 5秒間隔（60FPS基準）
            {
                csvOutputCounter = 0;
                
                // MethodProfilerの統計をCSVに出力
                if (ProfilerManager.Instance?.CsvManager != null)
                {
                    ProfilerManager.OutputMethodStats();
                }
            }
        }

        private int GetDrawCalls()
        {
            // Unity 5.6では実際のDrawCall統計は取得不可
            // 推定値は作らず、取得できない場合は0を返す
            return 0;
        }

        private int GetTriangles()
        {
            // Unity 5.6では実際のTriangles統計は取得不可
            return 0;
        }

        private int GetSetPassCalls()
        {
            // Unity 5.6では実際のSetPassCalls統計は取得不可
            return 0;
        }

        private int GetBatches()
        {
            // Unity 5.6では実際のBatches統計は取得不可
            return 0;
        }

        private float GetGPUMemory()
        {
            // Unity 5.6でSystemInfo.graphicsMemorySizeは利用可能
            try
            {
                return SystemInfo.graphicsMemorySize; // MB単位
            }
            catch
            {
                return 0f;
            }
        }

        private string GetGPUName()
        {
            try
            {
                return SystemInfo.graphicsDeviceName;
            }
            catch
            {
                return "Unknown GPU";
            }
        }

        private int GetVertices()
        {
            // Unity 5.6では実際のVertices統計は取得不可
            return 0;
        }

        private long GetTotalMemory()
        {
            return System.GC.GetTotalMemory(false);
        }

        private long GetUsedMemory()
        {
            // Unity 5.6ではGC.GetTotalMemoryのみ利用可能
            return System.GC.GetTotalMemory(false);
        }

        // 以下のメソッドは削除 - Unity 5.6では利用不可

        public List<string> GetTopBottlenecks(int count)
        {
            var bottlenecks = new List<string>();

            if (CurrentData.FrameTime > 0)
            {
                float currentFPS = 1000f / CurrentData.FrameTime;
                
                if (currentFPS < FPSCriticalThreshold)
                {
                    bottlenecks.Add(string.Format("CRITICAL: Low FPS ({0:F1})", currentFPS));
                }
                else if (currentFPS < FPSWarningThreshold)
                {
                    bottlenecks.Add(string.Format("WARNING: Low FPS ({0:F1})", currentFPS));
                }

                if (CurrentData.DrawCalls > DrawCallCriticalThreshold)
                {
                    bottlenecks.Add(string.Format("CRITICAL: High Draw Calls ({0})", CurrentData.DrawCalls));
                }
                else if (CurrentData.DrawCalls > DrawCallWarningThreshold)
                {
                    bottlenecks.Add(string.Format("WARNING: High Draw Calls ({0})", CurrentData.DrawCalls));
                }

                if (CurrentData.UsedMemory > MemoryWarningThreshold)
                {
                    bottlenecks.Add(string.Format("WARNING: High Memory Usage ({0}MB)", CurrentData.UsedMemory / (1024 * 1024)));
                }

                if (CurrentData.SimulationTime > CurrentData.FrameTime * 0.4f)
                {
                    bottlenecks.Add(string.Format("Simulation Heavy: {0:F1}%", (CurrentData.SimulationTime / CurrentData.FrameTime) * 100f));
                }

                if (CurrentData.RenderingTime > CurrentData.FrameTime * 0.4f)
                {
                    bottlenecks.Add(string.Format("Rendering Heavy: {0:F1}%", (CurrentData.RenderingTime / CurrentData.FrameTime) * 100f));
                }
            }

            // countの制限を適用
            if (bottlenecks.Count > count)
            {
                var result = new List<string>();
                for (int i = 0; i < count && i < bottlenecks.Count; i++)
                {
                    result.Add(bottlenecks[i]);
                }
                return result;
            }

            return bottlenecks;
        }

        public void Reset()
        {
            performanceHistory.Clear();
            isSimulationRunning = false;
            isRenderingRunning = false;
            isUIRunning = false;
        }

    }
}
