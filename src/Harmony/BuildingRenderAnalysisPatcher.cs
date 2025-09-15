using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using CS1Profiler.Core;
using System.Text;
using ColossalFramework;
using ColossalFramework.Plugins;
using System.Linq;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// Building.RenderInstance重複呼び出し分析パッチ
    /// BuildingManager.EndRenderingImpl全体を置き換えて詳細分析
    /// </summary>
    public static class BuildingRenderAnalysisPatcher
    {
        private static bool _isPatched = false;
        private static readonly object _patchLock = new object();
        
        /// <summary>
        /// パッチ適用
        /// </summary>
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            lock (_patchLock)
            {
                if (_isPatched) return;

                try
                {
                    var buildingManagerType = typeof(BuildingManager);
                    var endRenderingImplMethod = buildingManagerType.GetMethod("EndRenderingImpl", 
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    if (endRenderingImplMethod != null)
                    {
                        // EndRenderingImpl全体を置き換え
                        var replacement = new HarmonyMethod(typeof(BuildingRenderAnalysisHooks), "EndRenderingImpl_Replacement");
                        
                        harmony.Patch(endRenderingImplMethod, prefix: replacement);
                        _isPatched = true;
                        
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} BuildingRenderAnalysis patch applied to BuildingManager.EndRenderingImpl (full replacement)");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} BuildingManager.EndRenderingImpl method not found");
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} BuildingRenderAnalysisPatcher.ApplyPatches failed: {e.Message}");
                }
            }
        }

        /// <summary>
        /// パッチ削除
        /// </summary>
        public static void RemovePatches(HarmonyLib.Harmony harmony)
        {
            lock (_patchLock)
            {
                if (!_isPatched) return;

                try
                {
                    var buildingManagerType = typeof(BuildingManager);
                    var endRenderingImplMethod = buildingManagerType.GetMethod("EndRenderingImpl", 
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    if (endRenderingImplMethod != null)
                    {
                        harmony.Unpatch(endRenderingImplMethod, typeof(BuildingRenderAnalysisHooks).GetMethod("EndRenderingImpl_Replacement"));
                    }

                    _isPatched = false;
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} BuildingRenderAnalysis patches removed");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} BuildingRenderAnalysisPatcher.RemovePatches failed: {e.Message}");
                }
            }
        }

        public static bool IsPatched => _isPatched;
    }

    /// <summary>
    /// Building.RenderInstance呼び出し分析用Hooks
    /// </summary>
    public static class BuildingRenderAnalysisHooks
    {
        // 最小限の負荷でデータ収集
        private static readonly Dictionary<ushort, BuildingCallInfo> s_buildingCallStats = new Dictionary<ushort, BuildingCallInfo>();
        private static readonly object s_statsLock = new object();
        private static volatile bool s_isAnalyzing = false;
        private static int s_totalCalls = 0;
        private static int s_currentFrame = 0;
        
        // フレーム単位での詳細記録
        private static readonly List<FrameRenderData> s_frameDetails = new List<FrameRenderData>();
        private static FrameRenderData s_currentFrameData = null;
        private static bool s_recordFrameDetails = false;
        private static int s_maxFramesToRecord = 10; // 最新10フレームのみ記録
        
        // フレーム検出用
        private static int s_lastUnityFrame = -1;
        private static float s_lastTime = 0f;
        
        // RenderGroup可視化用（45x45グリッド）
        private static bool[,] s_currentRenderGroups = new bool[45, 45];
        private static readonly object s_renderGroupLock = new object();

        /// <summary>
        /// EndRenderingImpl完全置き換え（分析機能付き）
        /// </summary>
        public static bool EndRenderingImpl_Replacement(BuildingManager __instance, RenderManager.CameraInfo cameraInfo)
        {
            // カメラ情報を記録（フレーム詳細記録時）
            if (s_recordFrameDetails && s_currentFrameData != null)
            {
                s_currentFrameData.CameraPosition = cameraInfo.m_position;
                s_currentFrameData.CameraDirection = cameraInfo.m_forward;
            }

            // 元のEndRenderingImplの処理をコピーして分析処理を追加
            FastList<RenderGroup> renderedGroups = Singleton<RenderManager>.instance.m_renderedGroups;
            
            // RenderGroup状態をリセット
            if (s_recordFrameDetails)
            {
                lock (s_renderGroupLock)
                {
                    for (int x = 0; x < 45; x++)
                    {
                        for (int z = 0; z < 45; z++)
                        {
                            s_currentRenderGroups[x, z] = false;
                        }
                    }
                }
            }
            
            for (int i = 0; i < renderedGroups.m_size; i++)
            {
                RenderGroup renderGroup = renderedGroups.m_buffer[i];
                int num = renderGroup.m_layersRendered & ~(1 << Singleton<NotificationManager>.instance.m_notificationLayer);
                
                // RenderGroupの状態を記録
                if (s_recordFrameDetails && renderGroup.m_x >= 0 && renderGroup.m_x < 45 && renderGroup.m_z >= 0 && renderGroup.m_z < 45)
                {
                    lock (s_renderGroupLock)
                    {
                        s_currentRenderGroups[renderGroup.m_x, renderGroup.m_z] = true;
                    }
                }
                
                // High Detail Pass (instanceMask != 0)
                if (renderGroup.m_instanceMask != 0)
                {
                    num &= ~renderGroup.m_instanceMask;
                    int num2 = renderGroup.m_x * 270 / 45;
                    int num3 = renderGroup.m_z * 270 / 45;
                    int num4 = (renderGroup.m_x + 1) * 270 / 45 - 1;
                    int num5 = (renderGroup.m_z + 1) * 270 / 45 - 1;
                    
                    for (int j = num3; j <= num5; j++)
                    {
                        for (int k = num2; k <= num4; k++)
                        {
                            int num6 = j * 270 + k;
                            ushort num7 = __instance.m_buildingGrid[num6];
                            int num8 = 0;
                            
                            while (num7 != 0)
                            {
                                // 分析記録：High Detail Pass
                                LogBuildingRenderCall(num7, "HighDetail", renderGroup.m_x, renderGroup.m_z, num6, renderGroup.m_layersRendered, renderGroup.m_instanceMask);
                                
                                // 元の処理
                                __instance.m_buildings.m_buffer[(int)num7].RenderInstance(cameraInfo, num7, num | renderGroup.m_instanceMask);
                                num7 = __instance.m_buildings.m_buffer[(int)num7].m_nextGridBuilding;
                                
                                if (++num8 >= 49152)
                                {
                                    UnityEngine.Debug.LogError("Building analysis: Invalid list detected in high detail pass");
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // Low Detail Pass (num != 0)
                if (num != 0)
                {
                    int num9 = renderGroup.m_z * 45 + renderGroup.m_x;
                    ushort num10 = __instance.m_buildingGrid2[num9];
                    int num11 = 0;
                    
                    while (num10 != 0)
                    {
                        // 分析記録：Low Detail Pass
                        LogBuildingRenderCall(num10, "LowDetail", renderGroup.m_x, renderGroup.m_z, num9, renderGroup.m_layersRendered, 0);
                        
                        // 元の処理
                        __instance.m_buildings.m_buffer[(int)num10].RenderInstance(cameraInfo, num10, num);
                        num10 = __instance.m_buildings.m_buffer[(int)num10].m_nextGridBuilding2;
                        
                        if (++num11 >= 49152)
                        {
                            UnityEngine.Debug.LogError("Building analysis: Invalid list detected in low detail pass");
                            break;
                        }
                    }
                }
            }

            // 残りの元の処理（BuildingInfo更新等）
            int num12 = PrefabCollection<BuildingInfo>.PrefabCount();
            for (int l = 0; l < num12; l++)
            {
                BuildingInfo prefab = PrefabCollection<BuildingInfo>.GetPrefab((uint)l);
                if (prefab != null)
                {
                    prefab.UpdatePrefabInstances();
                    if (prefab.m_rendered)
                    {
                        if (prefab.m_lodCount != 0)
                        {
                            Building.RenderLod(cameraInfo, prefab);
                        }
                        if (prefab.m_subMeshes != null)
                        {
                            for (int m = 0; m < prefab.m_subMeshes.Length; m++)
                            {
                                BuildingInfoSub buildingInfoSub = prefab.m_subMeshes[m].m_subInfo as BuildingInfoSub;
                                if (buildingInfoSub.m_rendered)
                                {
                                    if (buildingInfoSub.m_lodCount != 0)
                                    {
                                        Building.RenderLod(cameraInfo, buildingInfoSub);
                                    }
                                    if (buildingInfoSub.m_subMeshes != null)
                                    {
                                        for (int n = 0; n < buildingInfoSub.m_subMeshes.Length; n++)
                                        {
                                            BuildingInfoSub buildingInfoSub2 = buildingInfoSub.m_subMeshes[n].m_subInfo as BuildingInfoSub;
                                            if (buildingInfoSub2.m_lodCount != 0)
                                            {
                                                Building.RenderLod(cameraInfo, buildingInfoSub2);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (__instance.m_common != null && __instance.m_common.m_subInfos != null)
            {
                for (int num13 = 0; num13 < __instance.m_common.m_subInfos.m_size; num13++)
                {
                    BuildingInfoBase buildingInfoBase = __instance.m_common.m_subInfos.m_buffer[num13];
                    if (buildingInfoBase.m_lodCount != 0)
                    {
                        Building.RenderLod(cameraInfo, buildingInfoBase);
                    }
                }
            }

            // フレーム終了時の処理
            if (s_recordFrameDetails)
            {
                FinalizeCurrentFrame();
            }

            // Harmonyのプレフィックスとして動作し、元のメソッドをスキップ
            return false;
        }

        /// <summary>
        /// Building.RenderInstance呼び出しログ（完全なコンテキスト付き）
        /// </summary>
        private static void LogBuildingRenderCall(ushort buildingID, string passType, int groupX, int groupZ, int gridIndex, int layersRendered = 0, int instanceMask = 0)
        {
            if (!s_isAnalyzing) return;

            // フレーム検出（Unity側のフレーム番号を使用）
            int currentUnityFrame = Time.frameCount;
            if (currentUnityFrame != s_lastUnityFrame)
            {
                s_lastUnityFrame = currentUnityFrame;
                s_currentFrame++;
                
                // フレーム詳細記録中なら前のフレームを確定
                if (s_recordFrameDetails && s_currentFrameData != null)
                {
                    FinalizeCurrentFrame();
                }
            }

            s_totalCalls++;

            // フレーム詳細記録
            if (s_recordFrameDetails)
            {
                // 新しいフレームの開始を検出
                if (s_currentFrameData == null || s_currentFrameData.FrameNumber != s_currentFrame)
                {
                    s_currentFrameData = new FrameRenderData
                    {
                        FrameNumber = s_currentFrame,
                        StartTime = Time.realtimeSinceStartup,
                        RenderCalls = new List<RenderCallDetail>()
                    };
                }
                
                // 建物位置とカメラからの距離を計算
                Vector3 buildingPos = Vector3.zero;
                float distanceToCamera = 0f;
                
                try
                {
                    if (BuildingManager.instance?.m_buildings?.m_buffer != null && 
                        buildingID < BuildingManager.instance.m_buildings.m_buffer.Length)
                    {
                        var building = BuildingManager.instance.m_buildings.m_buffer[buildingID];
                        buildingPos = building.m_position;
                        distanceToCamera = Vector3.Distance(buildingPos, s_currentFrameData.CameraPosition);
                    }
                }
                catch
                {
                    // エラー時はデフォルト値を使用
                }
                
                // 詳細な呼び出し情報を記録
                s_currentFrameData.RenderCalls.Add(new RenderCallDetail
                {
                    BuildingID = buildingID,
                    PassType = passType,
                    GroupX = groupX,
                    GroupZ = groupZ,
                    GridIndex = gridIndex,
                    Timestamp = Time.realtimeSinceStartup,
                    BuildingPosition = buildingPos,
                    DistanceToCamera = distanceToCamera,
                    LayersRendered = layersRendered,
                    InstanceMask = instanceMask
                });
            }

            // 高速パス：ロックを最小限に
            lock (s_statsLock)
            {
                if (!s_buildingCallStats.TryGetValue(buildingID, out BuildingCallInfo info))
                {
                    info = new BuildingCallInfo();
                    s_buildingCallStats[buildingID] = info;
                }
                
                info.AddCall(passType, groupX, groupZ, gridIndex);
            }
        }

        /// <summary>
        /// 分析開始（外部から呼び出し可能）
        /// </summary>
        public static void StartAnalysis()
        {
            lock (s_statsLock)
            {
                s_buildingCallStats.Clear();
                s_totalCalls = 0;
                s_currentFrame = 0;
                s_frameDetails.Clear();
                s_currentFrameData = null;
                s_lastUnityFrame = -1;
                s_lastTime = Time.realtimeSinceStartup;
                s_isAnalyzing = true;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building RenderInstance analysis started - manual control");
            }
        }

        /// <summary>
        /// フレーム詳細記録を開始
        /// </summary>
        public static void StartFrameDetailRecording()
        {
            s_recordFrameDetails = true;
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Frame detail recording enabled");
        }

        /// <summary>
        /// フレーム詳細記録を停止
        /// </summary>
        public static void StopFrameDetailRecording()
        {
            s_recordFrameDetails = false;
            FinalizeCurrentFrame();
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Frame detail recording disabled");
        }

        /// <summary>
        /// 現在のフレームデータを確定
        /// </summary>
        private static void FinalizeCurrentFrame()
        {
            if (s_currentFrameData != null)
            {
                s_currentFrameData.EndTime = Time.realtimeSinceStartup;
                
                // RenderGroupグリッドをコピー
                lock (s_renderGroupLock)
                {
                    for (int x = 0; x < 45; x++)
                    {
                        for (int z = 0; z < 45; z++)
                        {
                            s_currentFrameData.RenderGroupGrid[x, z] = s_currentRenderGroups[x, z];
                        }
                    }
                }
                
                // 建物別カウントを集計
                foreach (var call in s_currentFrameData.RenderCalls)
                {
                    if (!s_currentFrameData.BuildingCallCounts.ContainsKey(call.BuildingID))
                        s_currentFrameData.BuildingCallCounts[call.BuildingID] = 0;
                    s_currentFrameData.BuildingCallCounts[call.BuildingID]++;
                    
                    // 建物名を取得
                    call.BuildingName = GetBuildingName(call.BuildingID);
                }
                
                lock (s_statsLock)
                {
                    s_frameDetails.Add(s_currentFrameData);
                    
                    // 最大記録数を超えたら古いものを削除
                    while (s_frameDetails.Count > s_maxFramesToRecord)
                    {
                        s_frameDetails.RemoveAt(0);
                    }
                }
                
                s_currentFrameData = null;
                s_currentFrame++;
            }
        }

        /// <summary>
        /// 分析停止（外部から呼び出し可能）
        /// </summary>
        public static void StopAnalysis()
        {
            s_isAnalyzing = false;
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building RenderInstance analysis stopped - manual control");
        }

        /// <summary>
        /// フレーム詳細データを出力
        /// </summary>
        public static void OutputFrameDetails()
        {
            if (s_frameDetails.Count == 0)
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} No frame details recorded");
                return;
            }

            var output = new StringBuilder();
            output.AppendLine("=== Frame-by-Frame Building Render Analysis ===");
            output.AppendLine($"Recorded Frames: {s_frameDetails.Count}");
            output.AppendLine("");

            lock (s_statsLock)
            {
                foreach (var frameData in s_frameDetails)
                {
                    output.AppendLine($"Frame {frameData.FrameNumber}:");
                    output.AppendLine($"  Duration: {frameData.FrameDuration * 1000:F2}ms");
                    output.AppendLine($"  Total Calls: {frameData.TotalCalls}");
                    output.AppendLine($"  Unique Buildings: {frameData.UniqueBuildings}");
                    output.AppendLine($"  Camera Position: ({frameData.CameraPosition.x:F0}, {frameData.CameraPosition.y:F0}, {frameData.CameraPosition.z:F0})");
                    output.AppendLine($"  Camera Direction: ({frameData.CameraDirection.x:F2}, {frameData.CameraDirection.y:F2}, {frameData.CameraDirection.z:F2})");
                    
                    // 距離別分析
                    var distanceGroups = frameData.RenderCalls
                        .Where(c => c.DistanceToCamera > 0)
                        .GroupBy(c => 
                            c.DistanceToCamera < 500 ? "Near (<500m)" :
                            c.DistanceToCamera < 1000 ? "Medium (500-1000m)" :
                            c.DistanceToCamera < 2000 ? "Far (1000-2000m)" : "VeryFar (>2000m)")
                        .ToList();
                    
                    output.AppendLine("  Distance Analysis:");
                    foreach (var group in distanceGroups.OrderBy(g => g.Key))
                    {
                        output.AppendLine($"    {group.Key}: {group.Count()} buildings");
                    }
                    
                    // RenderGroupグリッドの可視化（45x45アスキーアート）
                    output.AppendLine("  RenderGroup Grid (45x45) - '#' = Rendered, '.' = Not Rendered:");
                    output.AppendLine("    Z→ 0    5    10   15   20   25   30   35   40   44");
                    output.AppendLine("  X↓ +----|----|----|----|----|----|----|----|----|----+");
                    
                    for (int z = 0; z < 45; z += 1) // 全行表示は長すぎるので間引く
                    {
                        if (z % 5 == 0 || z == 44) // 5行ごと＋最終行のみ表示
                        {
                            var line = new StringBuilder();
                            line.Append($"{z:D2}  | ");
                            for (int x = 0; x < 45; x++)
                            {
                                char symbol = frameData.RenderGroupGrid[x, z] ? '#' : '.';
                                line.Append(symbol);
                                if ((x + 1) % 5 == 0 && x < 44) line.Append('|'); // 5文字ごとに区切り
                            }
                            line.Append(" |");
                            output.AppendLine($"  {line}");
                        }
                    }
                    output.AppendLine("    +----|----|----|----|----|----|----|----|----|----+");
                    
                    // レンダリングされたRenderGroupの統計
                    int renderedGroups = 0;
                    for (int x = 0; x < 45; x++)
                    {
                        for (int z = 0; z < 45; z++)
                        {
                            if (frameData.RenderGroupGrid[x, z]) renderedGroups++;
                        }
                    }
                    output.AppendLine($"  Rendered Groups: {renderedGroups}/2025 ({renderedGroups * 100.0 / 2025:F1}%)");
                    
                    // 呼び出し回数が多い建物Top10
                    var topBuildings = frameData.BuildingCallCounts
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(10)
                        .ToList();
                    
                    output.AppendLine("  Top Buildings by Call Count:");
                    foreach (var building in topBuildings)
                    {
                        string buildingName = GetBuildingName(building.Key);
                        var buildingCall = frameData.RenderCalls.FirstOrDefault(c => c.BuildingID == building.Key);
                        float distance = buildingCall?.DistanceToCamera ?? 0;
                        output.AppendLine($"    {building.Key} ({buildingName}): {building.Value} calls, {distance:F0}m");
                    }
                    
                    // 異常な重複呼び出しを検出
                    var duplicates = frameData.BuildingCallCounts.Where(kvp => kvp.Value > 5).ToList();
                    if (duplicates.Any())
                    {
                        output.AppendLine("  ⚠️ Buildings with >5 calls (potential duplicates):");
                        foreach (var dup in duplicates)
                        {
                            string buildingName = GetBuildingName(dup.Key);
                            var buildingCall = frameData.RenderCalls.FirstOrDefault(c => c.BuildingID == dup.Key);
                            float distance = buildingCall?.DistanceToCamera ?? 0;
                            output.AppendLine($"    {dup.Key} ({buildingName}): {dup.Value} calls, {distance:F0}m");
                        }
                    }
                    
                    output.AppendLine("");
                }
                
                // 最新フレームの詳細呼び出しリスト
                if (s_frameDetails.Count > 0)
                {
                    var latestFrame = s_frameDetails.Last();
                    output.AppendLine($"Latest Frame ({latestFrame.FrameNumber}) Detailed Call List:");
                    output.AppendLine("BuildingID\tName\tPassType\tGroup\tGrid\tDistance\tTime");
                    output.AppendLine("".PadRight(100, '-'));
                    
                    // 距離順でソート
                    var sortedCalls = latestFrame.RenderCalls
                        .OrderBy(c => c.DistanceToCamera)
                        .Take(50) // 最大50件
                        .ToList();
                    
                    foreach (var call in sortedCalls)
                    {
                        output.AppendLine($"{call.BuildingID}\t{call.BuildingName}\t{call.PassType}\t({call.GroupX},{call.GroupZ})\t{call.GridIndex}\t{call.DistanceToCamera:F0}m\t{call.Timestamp:F3}");
                    }
                    
                    if (latestFrame.RenderCalls.Count > 50)
                    {
                        output.AppendLine($"... and {latestFrame.RenderCalls.Count - 50} more calls");
                    }
                }
            }

            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Frame Details:\n{output}");
            
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Frame Details:\n{output}");
            
            try
            {
                GUIUtility.systemCopyBuffer = output.ToString();
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Frame details copied to clipboard");
            }
            catch
            {
                // クリップボードアクセス失敗は無視
            }
        }

        /// <summary>
        /// 結果表示（ModTools用エイリアス）
        /// </summary>
        public static void PrintResults()
        {
            OutputStats();
        }

        /// <summary>
        /// フレーム詳細結果表示
        /// </summary>
        public static void PrintFrameDetails()
        {
            OutputFrameDetails();
        }

        /// <summary>
        /// カウンターリセット（外部から呼び出し可能）
        /// </summary>
        public static void ResetCounters()
        {
            lock (s_statsLock)
            {
                s_buildingCallStats.Clear();
                s_totalCalls = 0;
                s_currentFrame = 0;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building RenderInstance analysis counters reset");
            }
        }

        /// <summary>
        /// 統計出力（外部から呼び出し可能）
        /// 改善版：フレーム単位、建物名、距離情報を含む
        /// </summary>
        public static void OutputStats()
        {
            if (s_buildingCallStats.Count == 0)
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} No building render data collected");
                return;
            }

            var output = new StringBuilder();
            output.AppendLine("=== Building RenderInstance Analysis Results (Enhanced) ===");
            output.AppendLine($"Analysis Duration: {Time.realtimeSinceStartup - s_lastTime:F1} seconds");
            output.AppendLine($"Total Buildings Analyzed: {s_buildingCallStats.Count:N0}");
            output.AppendLine($"Total Frames Analyzed: {s_currentFrame:N0}");
            output.AppendLine($"Total RenderInstance Calls: {s_totalCalls:N0}");
            output.AppendLine($"Average Calls per Frame: {(s_currentFrame > 0 ? s_totalCalls / (float)s_currentFrame : 0):F1}");
            output.AppendLine($"Average Buildings per Frame: {(s_currentFrame > 0 ? s_buildingCallStats.Count / (float)s_currentFrame : 0):F1}");
            output.AppendLine("");

            // 呼び出し回数順でソート
            var sortedBuildings = new List<KeyValuePair<ushort, BuildingCallInfo>>(s_buildingCallStats);
            sortedBuildings.Sort((a, b) => b.Value.TotalCalls.CompareTo(a.Value.TotalCalls));

            // Top20の詳細（建物名と詳細情報を含む）
            output.AppendLine("Top 20 Buildings by Call Count:");
            output.AppendLine("ID\tName\tTotal\tHi/Frame\tLo/Frame\tHi%\tLo%\tGroups\tGrids");
            output.AppendLine("".PadRight(100, '-'));
            
            int count = 0;
            foreach (var kvp in sortedBuildings)
            {
                if (count >= 50) break;
                
                var buildingId = kvp.Key;
                var info = kvp.Value;
                
                // 建物名を取得
                string buildingName = GetBuildingName(buildingId);
                
                // フレーム単位の計算
                float hiPerFrame = info.HighDetailCalls / Math.Max(1f, s_currentFrame);
                float loPerFrame = info.LowDetailCalls / Math.Max(1f, s_currentFrame);
                float hiPercent = info.TotalCalls > 0 ? (info.HighDetailCalls * 100f / info.TotalCalls) : 0f;
                float loPercent = info.TotalCalls > 0 ? (info.LowDetailCalls * 100f / info.TotalCalls) : 0f;
                
                output.AppendLine($"{buildingId}\t{buildingName}\t{info.TotalCalls}\t{hiPerFrame:F1}\t{loPerFrame:F1}\t{hiPercent:F1}%\t{loPercent:F1}%\t{info.RenderGroups.Count}\t{info.GridIndices.Count}");
                count++;
            }

            // 統計サマリー
            output.AppendLine("");
            output.AppendLine("Summary Statistics:");
            
            int totalCalls = 0;
            int totalHighDetail = 0;
            int totalLowDetail = 0;
            int totalUniqueGroups = 0;
            
            foreach (var info in s_buildingCallStats.Values)
            {
                totalCalls += info.TotalCalls;
                totalHighDetail += info.HighDetailCalls;
                totalLowDetail += info.LowDetailCalls;
                totalUniqueGroups += info.RenderGroups.Count;
            }

            float avgCallsPerFrame = totalCalls / Math.Max(1f, s_currentFrame);
            float avgBuildingsPerFrame = s_buildingCallStats.Count / Math.Max(1f, s_currentFrame);
            float avgCallsPerBuildingPerFrame = avgCallsPerFrame / Math.Max(1f, avgBuildingsPerFrame);

            output.AppendLine($"Total RenderInstance Calls: {totalCalls:N0}");
            output.AppendLine($"High Detail Calls: {totalHighDetail:N0} ({totalHighDetail * 100.0 / Math.Max(1, totalCalls):F1}%)");
            output.AppendLine($"Low Detail Calls: {totalLowDetail:N0} ({totalLowDetail * 100.0 / Math.Max(1, totalCalls):F1}%)");
            output.AppendLine("");
            output.AppendLine("Performance Analysis:");
            output.AppendLine($"Average Calls per Frame: {avgCallsPerFrame:F1}");
            output.AppendLine($"Average Buildings per Frame: {avgBuildingsPerFrame:F1}");
            output.AppendLine($"Average Calls per Building per Frame: {avgCallsPerBuildingPerFrame:F1}");
            output.AppendLine($"Average Groups per Building: {totalUniqueGroups / (double)s_buildingCallStats.Count:F1}");
            
            // 異常検知
            output.AppendLine("");
            output.AppendLine("Anomaly Detection:");
            if (totalHighDetail * 100.0 / Math.Max(1, totalCalls) > 80.0)
            {
                output.AppendLine("⚠️ WARNING: High Detail ratio > 80% (should be lower for distant views)");
            }
            if (avgCallsPerBuildingPerFrame > 10.0)
            {
                output.AppendLine("⚠️ WARNING: >10 calls per building per frame (indicates potential duplication)");
            }
            if (totalLowDetail == 0 && s_buildingCallStats.Count > 50)
            {
                output.AppendLine("⚠️ WARNING: No Low Detail calls detected (unusual for large building sets)");
            }

            // ログ出力
            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building Analysis Results:\n{output}");
            
            // クリップボードにもコピー（ModToolsからコピーしやすくするため）
            try
            {
                GUIUtility.systemCopyBuffer = output.ToString();
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Results copied to clipboard");
            }
            catch
            {
                // クリップボードアクセス失敗は無視
            }
        }

        /// <summary>
        /// 建物名を取得
        /// </summary>
        private static string GetBuildingName(ushort buildingId)
        {
            try
            {
                if (BuildingManager.instance?.m_buildings?.m_buffer != null && buildingId < BuildingManager.instance.m_buildings.m_buffer.Length)
                {
                    var building = BuildingManager.instance.m_buildings.m_buffer[buildingId];
                    if (building.Info != null)
                    {
                        string name = building.Info.name;
                        // 長い名前は短縮
                        return name.Length > 20 ? name.Substring(0, 17) + "..." : name;
                    }
                }
                return "Unknown";
            }
            catch
            {
                return "Error";
            }
        }

        /// <summary>
        /// 現在の分析状態
        /// </summary>
        public static bool IsAnalyzing => s_isAnalyzing;
        
        /// <summary>
        /// 収集データ数
        /// </summary>
        public static int CollectedDataCount => s_buildingCallStats.Count;
    }

    /// <summary>
    /// 建物呼び出し情報
    /// </summary>
    public class BuildingCallInfo
    {
        public int HighDetailCalls { get; private set; }
        public int LowDetailCalls { get; private set; }
        public int TotalCalls => HighDetailCalls + LowDetailCalls;
        public HashSet<string> RenderGroups { get; } = new HashSet<string>();
        public HashSet<int> GridIndices { get; } = new HashSet<int>();

        public void AddCall(string passType, int groupX, int groupZ, int gridIndex)
        {
            if (passType == "HighDetail")
                HighDetailCalls++;
            else
                LowDetailCalls++;
                
            RenderGroups.Add($"{groupX},{groupZ}");
            GridIndices.Add(gridIndex);
        }
    }

    /// <summary>
    /// フレーム単位でのレンダリング詳細データ
    /// </summary>
    public class FrameRenderData
    {
        public int FrameNumber { get; set; }
        public float StartTime { get; set; }
        public float EndTime { get; set; }
        public List<RenderCallDetail> RenderCalls { get; set; } = new List<RenderCallDetail>();
        public Dictionary<ushort, int> BuildingCallCounts { get; set; } = new Dictionary<ushort, int>();
        public Vector3 CameraPosition { get; set; }
        public Vector3 CameraDirection { get; set; }
        public bool[,] RenderGroupGrid { get; set; } = new bool[45, 45]; // 45x45 RenderGroupグリッド
        
        public float FrameDuration => EndTime - StartTime;
        public int TotalCalls => RenderCalls.Count;
        public int UniqueBuildings => BuildingCallCounts.Count;
    }

    /// <summary>
    /// 個別のレンダリング呼び出し詳細
    /// </summary>
    public class RenderCallDetail
    {
        public ushort BuildingID { get; set; }
        public string PassType { get; set; }
        public int GroupX { get; set; }
        public int GroupZ { get; set; }
        public int GridIndex { get; set; }
        public float Timestamp { get; set; }
        public string BuildingName { get; set; }
        public Vector3 BuildingPosition { get; set; }
        public float DistanceToCamera { get; set; }
        public int LayersRendered { get; set; }
        public int InstanceMask { get; set; }
        
        public override string ToString()
        {
            return $"Building {BuildingID} ({BuildingName}) - {PassType} at Group({GroupX},{GroupZ}) Grid{GridIndex} Dist:{DistanceToCamera:F0}m Layers:0x{LayersRendered:X} Mask:0x{InstanceMask:X}";
        }
    }
}