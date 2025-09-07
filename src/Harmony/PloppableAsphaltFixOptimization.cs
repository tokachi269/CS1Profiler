using System;
using System.Linq;
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
        private static DesaturationControl _cachedDesaturationControl;
        private static System.Reflection.FieldInfo _targetField;
        private static UITextureSprite _cachedTextureSprite; // テクスチャスプライトもキャッシュ
        private static bool _isFullyLoaded = false;
        private static bool _opacityFixed = false; // opacity修正済みフラグ
        
        // 動的プロップ対応
        private static int _lastPropCount = 0; // 前回のプロップ数
        private static int _lastBuildingCount = 0; // 前回の建物数
        
        // フレーム制御
        private static int _lastProcessedFrame = -1;
        private static int _uiCheckInterval = 60; // 最大最適化: 60フレームに1回UIチェック（opacity修正は一度のみ）
        private static int _lastApplyPropertiesFrame = -1;
        
        // 統計
        private static int _skippedHeavyInitCount = 0;
        private static int _optimizedUICallCount = 0;
        
        // 設定オプション
        public static int ApplyPropertiesInterval = 1800; // 1800フレーム間隔（約30秒）- 安全性とパフォーマンスのバランス
        
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
                // Prefixのみで十分な最適化と透明化防止を実現
                var prefixMethod = new HarmonyMethod(typeof(PloppableAsphaltFixOptimization), nameof(SpikeFixOnUpdatePrefix));
                _harmony.Patch(onUpdateMethod, prefixMethod);
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix.MainThread.OnUpdate SPIKE FIX patch applied (Prefix only)");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to patch PloppableAsphaltFix.MainThread.OnUpdate: {e.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// スパイク修正版OnUpdate実装
        /// 元の処理を完全に置き換えて最大限の最適化を実現
        /// </summary>
        public static bool SpikeFixOnUpdatePrefix(object __instance, float realTimeDelta, float simulationTimeDelta)
        {
            try
            {
                // 負荷軽減のため処理頻度を大幅制限
                int currentFrame = Time.frameCount;
                
                // 初期化状態の確認と処理（非常に低頻度）
                if (!_isFullyLoaded && currentFrame % 120 == 0) // 2秒に1回チェック
                {
                    HandleInitializationSafely();
                }
                
                // UI関連処理（大幅に頻度制限）
                if (currentFrame - _lastProcessedFrame >= _uiCheckInterval)
                {
                    _lastProcessedFrame = currentFrame;
                    OptimizedUIProcessing();
                }
                
                // 透明化防止のため初回チェックのみ
                if (!_isFullyLoaded)
                {
                    EnsurePropertiesApplied(currentFrame);
                }
                else
                {
                    // 新しいプロップ/建物追加時の動的対応（低頻度チェック）
                    if (currentFrame % 300 == 0) // 5秒に1回チェック
                    {
                        CheckForNewAssets(currentFrame);
                    }
                }
                
                return false; // 元のOnUpdateをスキップ（完全最適化）
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} SpikeFixOnUpdatePrefix error: {e.Message}");
                return true; // エラー時は元の処理を実行（安全性優先）
            }
        }
        
        /// <summary>
        /// 初期化処理を安全に実行
        /// </summary>
        private static void HandleInitializationSafely()
        {
            try
            {
                var simulationManager = SimulationManager.instance;
                var loadingManager = Singleton<LoadingManager>.instance;
                
                // 基本的な初期化状態確認
                if (simulationManager != null && loadingManager != null && loadingManager.m_loadingComplete)
                {
                    var ploppableAsphaltType = GetPloppableAsphaltType();
                    if (ploppableAsphaltType != null)
                    {
                        var loadedField = ploppableAsphaltType.GetField("Loaded", BindingFlags.Static | BindingFlags.Public);
                        if (loadedField != null)
                        {
                            bool loaded = (bool)loadedField.GetValue(null);
                            if (!loaded)
                            {
                                // 透明化防止のため初回のApplyPropertiesは確実に実行
                                var startTime = DateTime.Now;
                                OptimizedApplyProperties(ploppableAsphaltType);
                                var executionTime = (DateTime.Now - startTime).TotalMilliseconds;
                                
                                loadedField.SetValue(null, true);
                                _lastApplyPropertiesFrame = Time.frameCount;
                                
                                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix: Initial ApplyProperties executed in {executionTime:F2}ms (transparency prevention)");
                                _skippedHeavyInitCount++;
                            }
                            _isFullyLoaded = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} HandleInitializationSafely error: {e.Message}");
            }
        }
        
        /// <summary>
        /// 透明化防止のためのApplyProperties定期実行
        /// </summary>
        private static void EnsurePropertiesApplied(int currentFrame)
        {
            try
            {
                // ApplyPropertiesは初回のみ実行（元コードと同じ動作）
                if (!_isFullyLoaded)
                {
                    var ploppableAsphaltType = GetPloppableAsphaltType();
                    if (ploppableAsphaltType != null)
                    {
                        var loadedField = ploppableAsphaltType.GetField("Loaded", BindingFlags.Static | BindingFlags.Public);
                        if (loadedField != null && !(bool)loadedField.GetValue(null))
                        {
                            // 初回のみApplyPropertiesを実行（元コードと同じ）
                            OptimizedApplyProperties(ploppableAsphaltType);
                            loadedField.SetValue(null, true);
                            _isFullyLoaded = true;
                            
                            // 初期カウント設定（動的追加検出用）
                            _lastPropCount = PrefabCollection<PropInfo>.LoadedCount();
                            _lastBuildingCount = PrefabCollection<BuildingInfo>.LoadedCount();
                            
                            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix: ApplyProperties completed (one-time only). Initial counts - Props: {_lastPropCount}, Buildings: {_lastBuildingCount}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} EnsurePropertiesApplied error: {e.Message}");
            }
        }
        
        /// <summary>
        /// 最適化されたApplyProperties実行
        /// 視覚的な問題を防ぎつつパフォーマンスを向上
        /// </summary>
        private static void OptimizedApplyProperties(Type ploppableAsphaltType)
        {
            try
            {
                // ApplyPropertiesメソッドを検索（Public優先、見つからなければNonPublic）
                var applyPropertiesMethod = ploppableAsphaltType.GetMethod("ApplyProperties", BindingFlags.Static | BindingFlags.Public);
                if (applyPropertiesMethod == null)
                {
                    applyPropertiesMethod = ploppableAsphaltType.GetMethod("ApplyProperties", BindingFlags.Static | BindingFlags.NonPublic);
                }
                
                if (applyPropertiesMethod != null)
                {
                    var startTime = DateTime.Now;
                    applyPropertiesMethod.Invoke(null, null);
                    var executionTime = (DateTime.Now - startTime).TotalMilliseconds;
                    
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix: ApplyProperties executed in {executionTime:F2}ms");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} ApplyProperties method not found in PloppableAsphalt type");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} OptimizedApplyProperties error: {e.Message}");
            }
        }
        
        /// <summary>
        /// 最適化されたUI処理（元のロジックを維持しつつ条件付き実行）
        /// </summary>
        private static void OptimizedUIProcessing()
        {
            try
            {
                _optimizedUICallCount++;
                
                // OptionsPanel のキャッシュチェック（元と同じ条件）
                if (_cachedOptionsPanel == null)
                {
                    _cachedOptionsPanel = UIView.library?.Get("OptionsPanel");
                }
                
                // 元のコードと同じ条件: component != null && component.isVisible
                if (_cachedOptionsPanel != null && _cachedOptionsPanel.isVisible)
                {
                    // OptionsPanel表示中のみDesaturationControl処理を実行
                    HandleDesaturationControlOptimized();
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
        /// DesaturationControlの最適化処理（積極的キャッシュ利用）
        /// </summary>
        private static void HandleDesaturationControlOptimized()
        {
            try
            {
                // opacity修正済みの場合は何もしない（最大の最適化）
                if (_opacityFixed) return;
                
                // キャッシュされたDesaturationControlを使用
                if (_cachedDesaturationControl == null)
                {
                    _cachedDesaturationControl = UnityEngine.Object.FindObjectOfType<DesaturationControl>();
                }
                
                if (_cachedDesaturationControl != null)
                {
                    // プライベートフィールド m_Target を取得（リフレクション）
                    if (_targetField == null)
                    {
                        _targetField = typeof(DesaturationControl).GetField("m_Target", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    }
                    
                    if (_targetField != null)
                    {
                        // TextureSpriteもキャッシュして再利用
                        if (_cachedTextureSprite == null)
                        {
                            _cachedTextureSprite = _targetField.GetValue(_cachedDesaturationControl) as UITextureSprite;
                        }
                        
                        // opacity修正が必要な場合のみ実行
                        if (_cachedTextureSprite != null && _cachedTextureSprite.opacity != 0f)
                        {
                            _cachedTextureSprite.opacity = 0f;
                            _targetField.SetValue(_cachedDesaturationControl, _cachedTextureSprite);
                            _opacityFixed = true; // 修正完了フラグ
                            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix: DesaturationControl opacity fixed permanently");
                        }
                        else if (_cachedTextureSprite != null && _cachedTextureSprite.opacity == 0f)
                        {
                            _opacityFixed = true; // 既に修正済み
                            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix: DesaturationControl opacity already fixed");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} HandleDesaturationControlOptimized error: {e.Message}");
                // エラー時はキャッシュクリア
                _cachedDesaturationControl = null;
                _targetField = null;
                _cachedTextureSprite = null;
                _opacityFixed = false;
            }
        }
        
        /// <summary>
        /// 全キャッシュをクリア
        /// </summary>
        private static void ClearAllCaches()
        {
            _cachedOptionsPanel = null;
            _cachedDesaturationControl = null;
            _targetField = null;
            _cachedTextureSprite = null;
            _opacityFixed = false;
            _lastPropCount = 0;
            _lastBuildingCount = 0;
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
                            if (type.FullName == "PloppableAsphaltFix.PloppableAsphalt" || 
                                type.Name == "PloppableAsphalt")
                            {
                                return type;
                            }
                        }
                    }
                }
                
                UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} PloppableAsphalt type not found in any assembly");
                return null;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Error searching PloppableAsphalt type: {e.Message}");
                return null;
            }
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
        
        /// <summary>
        /// 新しいアセット（プロップ/建物）追加の動的チェック
        /// </summary>
        private static void CheckForNewAssets(int currentFrame)
        {
            try
            {
                int currentPropCount = PrefabCollection<PropInfo>.LoadedCount();
                int currentBuildingCount = PrefabCollection<BuildingInfo>.LoadedCount();
                
                // 新しいプロップまたは建物が追加された場合
                if (currentPropCount != _lastPropCount || currentBuildingCount != _lastBuildingCount)
                {
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix: New assets detected - Props: {_lastPropCount}→{currentPropCount}, Buildings: {_lastBuildingCount}→{currentBuildingCount}");
                    
                    // 新しいアセットに対してApplyPropertiesを実行
                    var ploppableAsphaltType = GetPloppableAsphaltType();
                    if (ploppableAsphaltType != null)
                    {
                        var startTime = DateTime.Now;
                        OptimizedApplyProperties(ploppableAsphaltType);
                        var executionTime = (DateTime.Now - startTime).TotalMilliseconds;
                        
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix: Dynamic ApplyProperties executed in {executionTime:F2}ms for new assets");
                    }
                    
                    // カウント更新
                    _lastPropCount = currentPropCount;
                    _lastBuildingCount = currentBuildingCount;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} CheckForNewAssets error: {e.Message}");
            }
        }
    }
}
