using ColossalFramework;
using System;
using System.Collections.Generic;
using UnityEngine;
using CS1Profiler.Core;

namespace CS1Profiler.Managers
{
    /// <summary>
    /// Propバッチング検証マネージャー
    /// パフォーマンステスト用のバッチング有効/無効切り替え機能
    /// </summary>
    public static class PropBatchingTestManager
    {
        private static readonly Dictionary<uint, Material> _originalLodMaterials = new Dictionary<uint, Material>();
        private static readonly Dictionary<uint, Material> _originalHighDetailMaterials = new Dictionary<uint, Material>();
        private static bool _batchingOverrideActive = false;
        private static bool _currentBatchingState = false;
        private static bool _highDetailBatchingEnabled = false;

        /// <summary>
        /// 現在のバッチング状態を取得
        /// </summary>
        public static bool IsBatchingEnabled => _currentBatchingState;

        /// <summary>
        /// オーバーライドが有効かどうか
        /// </summary>
        public static bool IsOverrideActive => _batchingOverrideActive;

        /// <summary>
        /// 現在の高解像度バッチング状態を取得
        /// </summary>
        public static bool IsHighDetailBatchingEnabled => _highDetailBatchingEnabled;

        /// <summary>
        /// Propバッチングの強制切り替え
        /// </summary>
        /// <param name="enableBatching">true: バッチング有効化, false: バッチング無効化</param>
        public static void SetPropBatching(bool enableBatching)
        {
            SetPropBatching(enableBatching, false);
        }

        /// <summary>
        /// Propバッチングの強制切り替え（拡張版）
        /// </summary>
        /// <param name="enableBatching">true: バッチング有効化, false: バッチング無効化</param>
        /// <param name="includeHighDetail">true: 近距離高解像度描画にもバッチング適用</param>
        public static void SetPropBatching(bool enableBatching, bool includeHighDetail)
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Setting prop batching to: {enableBatching}, includeHighDetail: {includeHighDetail}");

                if (!_batchingOverrideActive)
                {
                    // 初回実行時に元の状態を保存
                    BackupOriginalBatchingState();
                    _batchingOverrideActive = true;
                }

                int totalProps = 0;
                int modifiedLodProps = 0;
                int modifiedHighDetailProps = 0;

                for (uint i = 0; i < PrefabCollection<PropInfo>.PrefabCount(); i++)
                {
                    var propInfo = PrefabCollection<PropInfo>.GetPrefab(i);
                    if (propInfo != null)
                    {
                        totalProps++;

                        // LODバッチング処理
                        if (propInfo.m_lodMaterial != null)
                        {
                            if (enableBatching)
                            {
                                // バッチング強制有効化（既に有効でも強制設定）
                                if (propInfo.m_lodMaterialCombined == null)
                                {
                                    propInfo.m_lodMaterialCombined = propInfo.m_lodMaterial;
                                    InitializeLodArrays(propInfo);
                                    modifiedLodProps++;
                                }
                                else if (propInfo.m_lodMaterialCombined != propInfo.m_lodMaterial)
                                {
                                    // 既に設定されているが異なるマテリアルの場合は更新
                                    propInfo.m_lodMaterialCombined = propInfo.m_lodMaterial;
                                    modifiedLodProps++;
                                }
                            }
                            else
                            {
                                // バッチング強制無効化
                                if (propInfo.m_lodMaterialCombined != null)
                                {
                                    propInfo.m_lodMaterialCombined = null;
                                    modifiedLodProps++;
                                }
                            }
                        }

                        // 高解像度バッチング処理（実験的機能）
                        if (includeHighDetail && propInfo.m_mesh != null && propInfo.m_material != null)
                        {
                            if (enableBatching)
                            {
                                // 高解像度メッシュのバッチング化（実験的）
                                // LODメッシュとして扱うことで強制的にバッチング
                                if (propInfo.m_lodMesh == null)
                                {
                                    propInfo.m_lodMesh = propInfo.m_mesh;
                                    propInfo.m_lodMaterial = propInfo.m_material;
                                    propInfo.m_lodMaterialCombined = propInfo.m_material;
                                    InitializeLodArrays(propInfo);
                                    modifiedHighDetailProps++;
                                }
                            }
                            else
                            {
                                // 高解像度バッチングを無効化
                                // 元の状態に戻す
                                if (_originalHighDetailMaterials.ContainsKey(i))
                                {
                                    // 元の設定を復元
                                    RestoreHighDetailProp(propInfo, i);
                                    modifiedHighDetailProps++;
                                }
                            }
                        }
                    }
                }

                _currentBatchingState = enableBatching;
                _highDetailBatchingEnabled = includeHighDetail && enableBatching;
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Prop batching override completed:");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   Total props: {totalProps}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   Modified LOD props: {modifiedLodProps}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   Modified high-detail props: {modifiedHighDetailProps}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   Batching enabled: {enableBatching}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   High-detail batching: {includeHighDetail}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to set prop batching: {ex.Message}");
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 元の状態を復元
        /// </summary>
        public static void RestoreOriginalBatching()
        {
            if (!_batchingOverrideActive)
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} No batching override is active");
                return;
            }

            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Restoring original prop batching state...");

                int restoredProps = 0;

                foreach (var kvp in _originalLodMaterials)
                {
                    uint propIndex = kvp.Key;
                    Material originalMaterial = kvp.Value;

                    var propInfo = PrefabCollection<PropInfo>.GetPrefab(propIndex);
                    if (propInfo != null)
                    {
                        propInfo.m_lodMaterialCombined = originalMaterial;
                        restoredProps++;
                    }
                }

                _originalLodMaterials.Clear();
                _originalHighDetailMaterials.Clear();
                _batchingOverrideActive = false;
                _currentBatchingState = false;
                _highDetailBatchingEnabled = false;

                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Restored {restoredProps} props to original batching state");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to restore original batching: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のバッチング状態を分析
        /// </summary>
        public static void AnalyzeBatchingState()
        {
            try
            {
                int totalProps = 0;
                int batchingEnabledProps = 0;
                int lodMaterialProps = 0;
                int highDetailProps = 0;
                int originallyBatchedProps = 0; // 元々バッチングされていたProp数

                var propTypeStats = new Dictionary<string, BatchingStats>();

                for (uint i = 0; i < PrefabCollection<PropInfo>.PrefabCount(); i++)
                {
                    var propInfo = PrefabCollection<PropInfo>.GetPrefab(i);
                    if (propInfo != null)
                    {
                        totalProps++;
                        
                        if (propInfo.m_lodMaterial != null)
                        {
                            lodMaterialProps++;
                        }

                        if (propInfo.m_mesh != null)
                        {
                            highDetailProps++;
                        }

                        if (propInfo.m_lodMaterialCombined != null)
                        {
                            batchingEnabledProps++;
                        }

                        // 元の状態確認
                        if (_originalLodMaterials.ContainsKey(i))
                        {
                            if (_originalLodMaterials[i] != null)
                            {
                                originallyBatchedProps++;
                            }
                        }
                        else if (propInfo.m_lodMaterialCombined != null)
                        {
                            // バックアップがない場合は現在の状態が元の状態
                            originallyBatchedProps++;
                        }

                        // カテゴリ別統計
                        string category = GetPropCategory(propInfo);
                        if (!propTypeStats.ContainsKey(category))
                        {
                            propTypeStats[category] = new BatchingStats();
                        }

                        var stats = propTypeStats[category];
                        stats.TotalCount++;
                        if (propInfo.m_lodMaterialCombined != null)
                        {
                            stats.BatchingEnabledCount++;
                        }
                    }
                }

                // 結果出力
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} === Prop Batching Analysis ===");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Total Props: {totalProps}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Props with LOD Material: {lodMaterialProps} ({(float)lodMaterialProps / totalProps * 100:F1}%)");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Props with High Detail Mesh: {highDetailProps} ({(float)highDetailProps / totalProps * 100:F1}%)");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Currently Batching Enabled Props: {batchingEnabledProps} ({(float)batchingEnabledProps / totalProps * 100:F1}%)");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Originally Batched Props: {originallyBatchedProps} ({(float)originallyBatchedProps / totalProps * 100:F1}%)");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Override Active: {_batchingOverrideActive}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Current Settings: Batching={_currentBatchingState}, High-detail={_highDetailBatchingEnabled}");
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} === Category Breakdown ===");
                foreach (var kvp in propTypeStats)
                {
                    var stats = kvp.Value;
                    float percentage = stats.TotalCount > 0 ? (float)stats.BatchingEnabledCount / stats.TotalCount * 100 : 0;
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {kvp.Key}: {stats.BatchingEnabledCount}/{stats.TotalCount} ({percentage:F1}% batched)");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to analyze batching state: {ex.Message}");
            }
        }

        /// <summary>
        /// フレーム単位のパフォーマンス測定を開始
        /// </summary>
        public static void StartPerformanceBenchmark()
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Starting performance benchmark with current batching state...");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Current settings: Batching={_currentBatchingState}, High-detail={_highDetailBatchingEnabled}");
                
                // Building分析とフレーム記録を同時に開始
                CS1Profiler.Harmony.PatchController.BuildingRenderAnalysisEnabled = true;
                CS1Profiler.Harmony.BuildingRenderAnalysisHooks.ResetCounters();
                CS1Profiler.Harmony.BuildingRenderAnalysisHooks.StartAnalysis();
                CS1Profiler.Harmony.BuildingRenderAnalysisHooks.StartFrameDetailRecording();
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance benchmark started - measuring for 30 seconds...");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} You can now observe 1-frame performance in real-time logs");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to start performance benchmark: {ex.Message}");
            }
        }

        /// <summary>
        /// フレーム単位のパフォーマンス測定を停止
        /// </summary>
        public static void StopPerformanceBenchmark()
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Stopping performance benchmark...");
                
                // Building分析とフレーム記録を停止
                CS1Profiler.Harmony.BuildingRenderAnalysisHooks.StopFrameDetailRecording();
                CS1Profiler.Harmony.BuildingRenderAnalysisHooks.StopAnalysis();
                
                // 最終結果を表示
                CS1Profiler.Harmony.BuildingRenderAnalysisHooks.PrintResults();
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance benchmark completed");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Settings during test: Batching={_currentBatchingState}, High-detail={_highDetailBatchingEnabled}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to stop performance benchmark: {ex.Message}");
            }
        }

        /// <summary>
        /// 性能改善のための距離設定最適化
        /// </summary>
        /// <param name="enableOptimization">最適化を有効にするか</param>
        public static void OptimizeRenderDistances(bool enableOptimization)
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {(enableOptimization ? "Enabling" : "Disabling")} render distance optimization...");

                int totalProps = 0;
                int optimizedProps = 0;

                for (uint i = 0; i < PrefabCollection<PropInfo>.PrefabCount(); i++)
                {
                    var propInfo = PrefabCollection<PropInfo>.GetPrefab(i);
                    if (propInfo != null && propInfo.m_lodMaterial != null)
                    {
                        totalProps++;

                        if (enableOptimization)
                        {
                            // 元の値をバックアップ
                            if (!_batchingOverrideActive)
                            {
                                BackupOriginalBatchingState();
                                _batchingOverrideActive = true;
                            }

                            // LOD距離を短縮（より近くからLOD使用）
                            float originalLodDistance = propInfo.m_lodRenderDistance;
                            propInfo.m_lodRenderDistance = originalLodDistance * 0.5f; // 50%に短縮

                            // バッチングも強制有効化
                            if (propInfo.m_lodMaterialCombined == null)
                            {
                                propInfo.m_lodMaterialCombined = propInfo.m_lodMaterial;
                                InitializeLodArrays(propInfo);
                            }

                            optimizedProps++;
                        }
                        else
                        {
                            // 元の設定に戻す
                            if (_originalLodMaterials.ContainsKey(i))
                            {
                                propInfo.m_lodMaterialCombined = _originalLodMaterials[i];
                                // 距離は元に戻さない（PropInfoに元の値が保存されていないため）
                                optimizedProps++;
                            }
                        }
                    }
                }

                _currentBatchingState = enableOptimization;

                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Render distance optimization completed:");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   Total props processed: {totalProps}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   Optimized props: {optimizedProps}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   Optimization enabled: {enableOptimization}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to optimize render distances: {ex.Message}");
            }
        }

        /// <summary>
        /// 激しい最適化：視界外カリング強化
        /// </summary>
        /// <param name="enableAggressive">激しい最適化を有効にするか</param>
        public static void OptimizeAggressiveCulling(bool enableAggressive)
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {(enableAggressive ? "Enabling" : "Disabling")} aggressive culling optimization...");

                int totalProps = 0;
                int optimizedProps = 0;

                for (uint i = 0; i < PrefabCollection<PropInfo>.PrefabCount(); i++)
                {
                    var propInfo = PrefabCollection<PropInfo>.GetPrefab(i);
                    if (propInfo != null)
                    {
                        totalProps++;

                        if (enableAggressive)
                        {
                            // 元の値をバックアップ
                            if (!_batchingOverrideActive)
                            {
                                BackupOriginalBatchingState();
                                _batchingOverrideActive = true;
                            }

                            // 激しい距離短縮
                            propInfo.m_maxRenderDistance = propInfo.m_maxRenderDistance * 0.6f; // 40%短縮
                            propInfo.m_lodRenderDistance = propInfo.m_lodRenderDistance * 0.3f; // 70%短縮

                            // 小さいPropは更に短縮
                            if (propInfo.m_generatedInfo.m_size.x < 5f)
                            {
                                propInfo.m_maxRenderDistance = propInfo.m_maxRenderDistance * 0.5f;
                            }

                            // LODバッチング強制有効
                            if (propInfo.m_lodMaterial != null && propInfo.m_lodMaterialCombined == null)
                            {
                                propInfo.m_lodMaterialCombined = propInfo.m_lodMaterial;
                                InitializeLodArrays(propInfo);
                            }

                            optimizedProps++;
                        }
                    }
                }

                _currentBatchingState = enableAggressive;

                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Aggressive culling optimization completed:");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   Total props processed: {totalProps}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   Aggressively optimized props: {optimizedProps}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX}   WARNING: Visual quality significantly reduced for performance");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to optimize aggressive culling: {ex.Message}");
            }
        }

        private static void BackupOriginalBatchingState()
        {
            _originalLodMaterials.Clear();
            _originalHighDetailMaterials.Clear();

            for (uint i = 0; i < PrefabCollection<PropInfo>.PrefabCount(); i++)
            {
                var propInfo = PrefabCollection<PropInfo>.GetPrefab(i);
                if (propInfo != null)
                {
                    // LOD状態をバックアップ
                    _originalLodMaterials[i] = propInfo.m_lodMaterialCombined;
                    
                    // 高解像度状態をバックアップ（LODメッシュが存在するかどうか）
                    _originalHighDetailMaterials[i] = propInfo.m_lodMesh != null ? propInfo.m_lodMaterial : null;
                }
            }

            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Backed up original batching state for {_originalLodMaterials.Count} props");
        }

        private static void RestoreHighDetailProp(PropInfo propInfo, uint index)
        {
            // 高解像度Propの元の状態を復元
            // 実際には元々LODメッシュがなかった場合は削除
            var originalLodMaterial = _originalHighDetailMaterials[index];
            if (originalLodMaterial == null)
            {
                // 元々LODメッシュがなかった場合
                propInfo.m_lodMesh = null;
                propInfo.m_lodMaterial = null;
                propInfo.m_lodMaterialCombined = null;
            }
        }

        private static void InitializeLodArrays(PropInfo propInfo)
        {
            try
            {
                // LOD配列のサイズを決定（デフォルト16）
                int lodArraySize = 16;

                // LOD配列を初期化
                if (propInfo.m_lodLocations == null || propInfo.m_lodLocations.Length != lodArraySize)
                {
                    propInfo.m_lodLocations = new Vector4[lodArraySize];
                }

                if (propInfo.m_lodObjectIndices == null || propInfo.m_lodObjectIndices.Length != lodArraySize)
                {
                    propInfo.m_lodObjectIndices = new Vector4[lodArraySize];
                }

                if (propInfo.m_lodColors == null || propInfo.m_lodColors.Length != lodArraySize)
                {
                    propInfo.m_lodColors = new Vector4[lodArraySize];
                }

                // バウンド初期化
                propInfo.m_lodMin = new Vector3(100000f, 100000f, 100000f);
                propInfo.m_lodMax = new Vector3(-100000f, -100000f, -100000f);
                propInfo.m_lodCount = 0;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Failed to initialize LOD arrays for {propInfo.name}: {ex.Message}");
            }
        }

        private static string GetPropCategory(PropInfo propInfo)
        {
            if (propInfo.name == null) return "Unknown";

            string name = propInfo.name.ToLower();
            
            if (name.Contains("tree") || name.Contains("bush") || name.Contains("plant"))
                return "Vegetation";
            else if (name.Contains("street") || name.Contains("road") || name.Contains("light"))
                return "Street";
            else if (name.Contains("park") || name.Contains("bench") || name.Contains("fountain"))
                return "Park";
            else if (name.Contains("industrial") || name.Contains("factory"))
                return "Industrial";
            else if (name.Contains("commercial") || name.Contains("shop"))
                return "Commercial";
            else if (name.Contains("residential") || name.Contains("house"))
                return "Residential";
            else if (name.Contains("effect") || name.Contains("particle"))
                return "Effects";
            else
                return "Other";
        }

        private class BatchingStats
        {
            public int TotalCount { get; set; }
            public int BatchingEnabledCount { get; set; }
        }
    }
}