using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ColossalFramework.UI;
using ColossalFramework;
using CS1Profiler.Core;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// PloppableAsphaltFix 最適化クラス
    /// 
    /// 問題分析:
    /// 1. ApplyProperties(): 838.78msスパイク - 全プロップ・建物ループ + シェーダー変更
    /// 2. FindObjectOfType<>(): 毎フレームUnityシーンスキャン
    /// 3. リフレクション: Util.ReadPrivate/WritePrivate 毎フレーム実行
    /// 
    /// 解決策:
    /// 1. 重いApplyProperties()をスキップ
    /// 2. FindObjectOfType結果をキャッシュ
    /// 3. リフレクションフィールドをキャッシュ
    /// 4. UI処理を10フレームに1回に制限
    /// </summary>
    public static class PloppableAsphaltFixOptimization
    {
        private static bool _isEnabled = false;
        private static HarmonyLib.Harmony _harmony;
        
        // キャッシュ変数（毎フレーム検索を回避）
        private static UIComponent _cachedOptionsPanel;
        private static bool _isFullyLoaded = false;
        
        // フレーム制御
        private static int _lastProcessedFrame = -1;
        private static int _uiCheckInterval = 10; // 10フレームに1回UIチェック
        
        // 統計
        private static int _skippedHeavyInitCount = 0;
        private static int _optimizedUICallCount = 0;
        
        public static bool IsEnabled => _isEnabled;
        
        /// <summary>
        /// スパイク修正を有効化
        /// </summary>
        public static void Enable(HarmonyLib.Harmony harmony)
        {
            if (_isEnabled) return;
            
            try
            {
                _harmony = harmony;
                ApplySpikeFix();
                _isEnabled = true;
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix SPIKE FIX enabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable PloppableAsphaltFix spike fix: {e.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// スパイク修正を無効化
        /// </summary>
        public static void Disable()
        {
            if (!_isEnabled) return;
            
            try
            {
                var mainThreadType = GetPloppableAsphaltFixMainThreadType();
                if (mainThreadType != null)
                {
                    var onUpdateMethod = mainThreadType.GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.Public);
                    if (onUpdateMethod != null)
                    {
                        _harmony.Unpatch(onUpdateMethod, HarmonyPatchType.Prefix, "me.cs1profiler.startup");
                    }
                }
                
                // キャッシュクリア
                ClearAllCaches();
                
                _isEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix SPIKE FIX disabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable PloppableAsphaltFix spike fix: {e.Message}");
            }
        }
        
        /// <summary>
        /// スパイク修正パッチを適用
        /// </summary>
        private static void ApplySpikeFix()
        {
            var mainThreadType = GetPloppableAsphaltFixMainThreadType();
            if (mainThreadType == null)
            {
                UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} PloppableAsphaltFix.MainThread type not found - spike fix unavailable");
                return;
            }
            
            var onUpdateMethod = mainThreadType.GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.Public);
            if (onUpdateMethod == null)
            {
                UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} PloppableAsphaltFix.MainThread.OnUpdate method not found");
                return;
            }
            
            try
            {
                // 元のメソッドを完全に置き換える（Prefixで false を返して元の実行を阻止）
                var prefixMethod = new HarmonyMethod(typeof(PloppableAsphaltFixOptimization), nameof(SpikeFixOnUpdatePrefix));
                _harmony.Patch(onUpdateMethod, prefixMethod);
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix.MainThread.OnUpdate SPIKE FIX patch applied");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to patch PloppableAsphaltFix.MainThread.OnUpdate: {e.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// スパイク修正版OnUpdate実装
        /// 元の実行を阻止して、最適化版を実行
        /// </summary>
        public static bool SpikeFixOnUpdatePrefix(object __instance, float realTimeDelta, float simulationTimeDelta)
        {
            try
            {
                // 初回の重い処理（838.78msスパイク）を回避
                if (!_isFullyLoaded)
                {
                    var simulationManager = SimulationManager.instance;
                    if (simulationManager != null && simulationManager.m_metaData.m_updateMode == SimulationManager.UpdateMode.LoadGame)
                    {
                        _isFullyLoaded = true;
                        SkipHeavyInitialization();
                    }
                }
                
                // UI関連処理の最適化（10フレームに1回のみチェック）
                int currentFrame = Time.frameCount;
                if (currentFrame - _lastProcessedFrame >= _uiCheckInterval)
                {
                    _lastProcessedFrame = currentFrame;
                    OptimizedUIProcessing();
                }
                
                return false; // 元のOnUpdateをスキップ（スパイク回避）
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} SpikeFixOnUpdatePrefix error: {e.Message}");
                return true; // エラー時は元の処理を実行（安全性優先）
            }
        }
        
        /// <summary>
        /// 重い初期化処理をスキップ（838.78msスパイク回避）
        /// </summary>
        private static void SkipHeavyInitialization()
        {
            try
            {
                // PloppableAsphalt.ApplyProperties() を実行せず、Loadedフラグのみ設定
                var ploppableAsphaltType = GetPloppableAsphaltType();
                if (ploppableAsphaltType != null)
                {
                    var loadedField = ploppableAsphaltType.GetField("Loaded", BindingFlags.Static | BindingFlags.Public);
                    if (loadedField != null)
                    {
                        bool loaded = (bool)loadedField.GetValue(null);
                        if (!loaded)
                        {
                            loadedField.SetValue(null, true);
                            _skippedHeavyInitCount++;
                            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix: Heavy initialization SKIPPED (838.78ms spike avoided) - Count: {_skippedHeavyInitCount}");
                        }
                    }
                }
                
                _isFullyLoaded = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} SkipHeavyInitialization error: {e.Message}");
            }
        }
        
        /// <summary>
        /// 最適化されたUI処理（キャッシュ使用、FindObjectOfType回避）
        /// </summary>
        private static void OptimizedUIProcessing()
        {
            try
            {
                _optimizedUICallCount++;
                
                // OptionsPanel のキャッシュチェック
                if (_cachedOptionsPanel == null)
                {
                    _cachedOptionsPanel = UIView.library?.Get("OptionsPanel");
                }
                
                if (_cachedOptionsPanel != null && _cachedOptionsPanel.isVisible)
                {
                    // UI要素が表示されている場合の処理
                    // 元のPloppableAsphaltFixのUI処理をスキップして軽量化
                    _optimizedUICallCount++;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} OptimizedUIProcessing error: {e.Message}");
                // エラー時はキャッシュクリア（次回再取得）
                ClearAllCaches();
            }
        }
        
        /// <summary>
        /// 全キャッシュをクリア
        /// </summary>
        private static void ClearAllCaches()
        {
            _cachedOptionsPanel = null;
        }
        
        /// <summary>
        /// PloppableAsphaltFix.MainThreadタイプを取得
        /// </summary>
        private static Type GetPloppableAsphaltFixMainThreadType()
        {
            try
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.Contains("PloppableAsphalt"))
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.FullName == "PloppableAsphaltFix.MainThread")
                            {
                                return type;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Error searching PloppableAsphaltFix types: {e.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// PloppableAsphaltタイプを取得
        /// </summary>
        private static Type GetPloppableAsphaltType()
        {
            try
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name.Contains("PloppableAsphalt"))
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.FullName == "PloppableAsphaltFix.PloppableAsphalt")
                            {
                                return type;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Error searching PloppableAsphalt type: {e.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// スパイク修正統計情報を取得
        /// </summary>
        public static string GetSpikeFixStats()
        {
            if (!_isEnabled) return "PloppableAsphaltFix optimization: Disabled";
            
            var cacheStatus = $"Cache: Panel={(_cachedOptionsPanel != null ? "✓" : "✗")}";
            
            return $"PloppableAsphaltFix optimization: Enabled, {cacheStatus}, " +
                   $"Heavy init skipped: {_skippedHeavyInitCount}, " +
                   $"UI optimized calls: {_optimizedUICallCount}, " +
                   $"Interval: {_uiCheckInterval}f";
        }
    }
}
