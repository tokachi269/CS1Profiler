using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using CS1Profiler.Core;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// RenderManager最適化クラス（元のLateUpdate完全復元版）
    /// RenderManager.LateUpdate()系メソッドを元のソースコードと同じ処理に置き換える
    /// 
    /// 対象メソッド:
    /// 1. RenderManager.FpsBoosterLateUpdate() - FPS Booster MOD導入時
    /// 2. RenderManager.LateUpdate() - バニラゲーム時
    /// 
    /// 機能:
    /// - デュアルメソッドパッチングによる包括的最適化
    /// - 元のLateUpdateソースコードの完全復元
    /// - リフレクションによる参照問題の解消のみ
    /// </summary>
    public static class RenderManagerOptimization
    {
        private static bool _isEnabled = false;
        private static HarmonyLib.Harmony _harmony;
        
        // リフレクション結果のキャッシュ
        private static Type _prefabPoolType;
        private static FieldInfo _canCreateInstancesField;
        private static Type _infoManagerType;
        private static PropertyInfo _infoManagerInstanceProperty;
        private static MethodInfo _updateInfoModeMethod;
        private static Type _loadingManagerType;
        private static PropertyInfo _loadingManagerInstanceProperty;
        private static FieldInfo _loadingCompleteField;
        
        // RenderManager関連のキャッシュ
        private static Type _renderManagerType;
        private static FieldInfo _currentFrameField;
        private static FieldInfo _outOfInstancesField;
        private static FieldInfo _lightSystemField;
        private static FieldInfo _overlayBufferField;
        private static MethodInfo _updateCameraInfoMethod;
        private static MethodInfo _updateColorMapMethod;
        private static FieldInfo _cameraInfoField;
        private static FieldInfo _renderablesField;
        private static FieldInfo _renderedGroupsField;
        private static FieldInfo _groupsField;
        private static FieldInfo _megaGroupsField;
        
        // FastList関連のキャッシュ
        private static MethodInfo _clearMethod;
        private static MethodInfo _addMethod;
        private static FieldInfo _sizeField;
        private static FieldInfo _bufferField;
        
        // LightSystem関連のキャッシュ
        private static MethodInfo _lightSystemEndRenderingMethod;
        
        // Renderables関連のキャッシュ（ループ内で使用される高負荷処理）
        private static FieldInfo _renderablesSizeField;
        private static FieldInfo _renderablesBufferField;
        private static MethodInfo _beginRenderingMethod;
        private static MethodInfo _endRenderingMethod;
        
        // CameraInfo関連のキャッシュ（フレーム単位で使用される）
        private static FieldInfo _cameraInfoBoundsField;
        private static PropertyInfo _boundsMinProperty;
        private static PropertyInfo _boundsMaxProperty;
        private static FieldInfo _cameraInfoShadowOffsetField;
        
        private static bool _cachesInitialized = false;
        
        public static bool IsEnabled => _isEnabled;
        
        /// <summary>
        /// RenderManager最適化を有効化
        /// </summary>
        public static void Enable(HarmonyLib.Harmony harmony)
        {
            if (_isEnabled) return;
            
            try
            {
                _harmony = harmony;
                InitializeCaches(); // リフレクションキャッシュを初期化
                ApplyRenderManagerPatch();
                _isEnabled = true;
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} RenderManager optimization patch enabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable RenderManager patch: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// RenderManager最適化を無効化
        /// </summary>
        public static void Disable()
        {
            if (!_isEnabled) return;
            
            try
            {
                var renderManagerType = GetRenderManagerType();
                if (renderManagerType != null)
                {
                    // FpsBoosterLateUpdate のパッチを解除
                    var fpsBoosterLateUpdateMethod = renderManagerType.GetMethod("FpsBoosterLateUpdate", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fpsBoosterLateUpdateMethod != null)
                    {
                        _harmony.Unpatch(fpsBoosterLateUpdateMethod, HarmonyPatchType.Transpiler, "me.cs1profiler.startup");
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} RenderManager.FpsBoosterLateUpdate optimization transpiler removed");
                    }
                    
                    // LateUpdate のパッチを解除
                    var lateUpdateMethod = renderManagerType.GetMethod("LateUpdate", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (lateUpdateMethod != null)
                    {
                        _harmony.Unpatch(lateUpdateMethod, HarmonyPatchType.Transpiler, "me.cs1profiler.startup");
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} RenderManager.LateUpdate transpiler patch removed");
                    }
                }
                
                _isEnabled = false;
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} RenderManager optimization patch disabled");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable RenderManager patch: {e.Message}");
            }
        }
        
        /// <summary>
        /// RenderManagerパッチを適用
        /// </summary>
        private static void ApplyRenderManagerPatch()
        {
            var renderManagerType = GetRenderManagerType();
            if (renderManagerType == null)
            {
                UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} RenderManager type not found - RenderManager patch unavailable");
                return;
            }
            
            bool patchApplied = false;
            
            // 1. FpsBoosterLateUpdate をパッチ（FPS Booster MOD導入時）
            var fpsBoosterLateUpdateMethod = renderManagerType.GetMethod("FpsBoosterLateUpdate", 
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fpsBoosterLateUpdateMethod != null)
            {
                try
                {
                    var transpilerMethod = new HarmonyMethod(typeof(RenderManagerOptimization), nameof(LateUpdateTranspiler));
                    
                    _harmony.Patch(fpsBoosterLateUpdateMethod, transpiler: transpilerMethod);
                    patchApplied = true;
                    
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} RenderManager.FpsBoosterLateUpdate optimization transpiler applied (FPS Booster MOD detected)");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to patch RenderManager.FpsBoosterLateUpdate: {e.Message}");
                }
            }
            
            // 2. LateUpdate をパッチ（バニラゲーム時）
            var lateUpdateMethod = renderManagerType.GetMethod("LateUpdate", 
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            // MonoBehaviourのLateUpdateも試す
            if (lateUpdateMethod == null)
            {
                lateUpdateMethod = renderManagerType.GetMethod("LateUpdate", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            }
            
            if (lateUpdateMethod != null)
            {
                try
                {
                    var transpilerMethod = new HarmonyMethod(typeof(RenderManagerOptimization), nameof(LateUpdateTranspiler));
                    
                    _harmony.Patch(lateUpdateMethod, transpiler: transpilerMethod);
                    patchApplied = true;
                    
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} RenderManager.LateUpdate optimization transpiler applied (vanilla game)");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to patch RenderManager.LateUpdate: {e.Message}");
                }
            }
            
            if (!patchApplied)
            {
                UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Neither FpsBoosterLateUpdate nor LateUpdate methods found in RenderManager");
            }
        }
        
        /// <summary>
        /// RenderManagerタイプを取得
        /// </summary>
        private static Type GetRenderManagerType()
        {
            try
            {
                // Assembly-CSharpからRenderManagerを検索
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "Assembly-CSharp")
                    {
                        var renderManagerType = assembly.GetType("RenderManager");
                        if (renderManagerType != null)
                        {
                            return renderManagerType;
                        }
                    }
                }
                
                UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} RenderManager type not found in Assembly-CSharp");
                return null;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Error searching RenderManager type: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 指定されたアセンブリからタイプを取得
        /// </summary>
        private static Type GetTypeFromAssembly(string typeName)
        {
            try
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "Assembly-CSharp")
                    {
                        var type = assembly.GetType(typeName);
                        if (type != null)
                        {
                            return type;
                        }
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Failed to get type {typeName}: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// リフレクション結果をキャッシュで初期化
        /// </summary>
        private static void InitializeCaches()
        {
            if (_cachesInitialized) return;
            
            try
            {
                // PrefabPool関連キャッシュ（安全に取得）
                try
                {
                    _prefabPoolType = GetTypeFromAssembly("PrefabPool");
                    if (_prefabPoolType != null)
                    {
                        _canCreateInstancesField = _prefabPoolType.GetField("m_canCreateInstances", 
                            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} PrefabPool cache initialization failed: {e.Message}");
                }
                
                // InfoManager関連キャッシュ
                _infoManagerType = GetTypeFromAssembly("InfoManager");
                if (_infoManagerType != null)
                {
                    _infoManagerInstanceProperty = _infoManagerType.GetProperty("instance", 
                        BindingFlags.Public | BindingFlags.Static);
                    _updateInfoModeMethod = _infoManagerType.GetMethod("UpdateInfoMode", 
                        BindingFlags.Public | BindingFlags.Instance);
                }
                
                // LoadingManager関連キャッシュ
                _loadingManagerType = GetTypeFromAssembly("LoadingManager");
                if (_loadingManagerType != null)
                {
                    _loadingManagerInstanceProperty = _loadingManagerType.GetProperty("instance", 
                        BindingFlags.Public | BindingFlags.Static);
                    _loadingCompleteField = _loadingManagerType.GetField("m_loadingComplete", 
                        BindingFlags.Public | BindingFlags.Instance);
                }
                
                // RenderManager関連キャッシュ
                _renderManagerType = GetRenderManagerType();
                if (_renderManagerType != null)
                {
                    _currentFrameField = _renderManagerType.GetField("m_currentFrame", BindingFlags.NonPublic | BindingFlags.Instance);
                    _outOfInstancesField = _renderManagerType.GetField("m_outOfInstances", BindingFlags.Public | BindingFlags.Instance);
                    _lightSystemField = _renderManagerType.GetField("m_lightSystem", BindingFlags.NonPublic | BindingFlags.Instance);
                    _overlayBufferField = _renderManagerType.GetField("m_overlayBuffer", BindingFlags.Public | BindingFlags.Instance);
                    _updateCameraInfoMethod = _renderManagerType.GetMethod("UpdateCameraInfo", BindingFlags.Public | BindingFlags.Instance);
                    _updateColorMapMethod = _renderManagerType.GetMethod("UpdateColorMap", BindingFlags.NonPublic | BindingFlags.Instance);
                    _cameraInfoField = _renderManagerType.GetField("m_cameraInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                    _renderablesField = _renderManagerType.GetField("m_renderables", BindingFlags.NonPublic | BindingFlags.Static);
                    _renderedGroupsField = _renderManagerType.GetField("m_renderedGroups", BindingFlags.Public | BindingFlags.Instance);
                    _groupsField = _renderManagerType.GetField("m_groups", BindingFlags.Public | BindingFlags.Instance);
                    _megaGroupsField = _renderManagerType.GetField("m_megaGroups", BindingFlags.Public | BindingFlags.Instance);
                }
                
                // FastList関連のキャッシュ（renderedGroups用）
                var fastListType = GetTypeFromAssembly("FastList`1");
                if (fastListType != null)
                {
                    var renderGroupType = GetTypeFromAssembly("RenderGroup");
                    if (renderGroupType != null)
                    {
                        var specificFastListType = fastListType.MakeGenericType(renderGroupType);
                        _clearMethod = specificFastListType.GetMethod("Clear");
                        _addMethod = specificFastListType.GetMethod("Add");
                        _sizeField = specificFastListType.GetField("m_size", BindingFlags.Public | BindingFlags.Instance);
                        _bufferField = specificFastListType.GetField("m_buffer", BindingFlags.Public | BindingFlags.Instance);
                    }
                }
                
                // LightSystem関連のキャッシュ
                var lightSystemType = GetTypeFromAssembly("LightSystem");
                if (lightSystemType != null)
                {
                    _lightSystemEndRenderingMethod = lightSystemType.GetMethod("EndRendering");
                }
                
                // Renderables関連のキャッシュ（最優先：ループ内で使用される）
                var renderablesType = GetTypeFromAssembly("FastList`1");
                if (renderablesType != null)
                {
                    var renderableType = GetTypeFromAssembly("IRenderable");
                    if (renderableType != null)
                    {
                        var specificRenderablesType = renderablesType.MakeGenericType(renderableType);
                        _renderablesSizeField = specificRenderablesType.GetField("m_size", BindingFlags.Public | BindingFlags.Instance);
                        _renderablesBufferField = specificRenderablesType.GetField("m_buffer", BindingFlags.Public | BindingFlags.Instance);
                    }
                    
                    // 共通のRendering関連メソッドキャッシュ
                    if (renderableType != null)
                    {
                        _beginRenderingMethod = renderableType.GetMethod("BeginRendering");
                        _endRenderingMethod = renderableType.GetMethod("EndRendering");
                    }
                }
                
                // CameraInfo関連のキャッシュ（フレーム単位で使用される）
                var cameraInfoType = GetTypeFromAssembly("RenderManager+CameraInfo");
                if (cameraInfoType != null)
                {
                    _cameraInfoBoundsField = cameraInfoType.GetField("m_bounds", BindingFlags.Public | BindingFlags.Instance);
                    _cameraInfoShadowOffsetField = cameraInfoType.GetField("m_shadowOffset", BindingFlags.Public | BindingFlags.Instance);
                    
                    // Bounds関連のプロパティ
                    var boundsType = typeof(UnityEngine.Bounds);
                    _boundsMinProperty = boundsType.GetProperty("min");
                    _boundsMaxProperty = boundsType.GetProperty("max");
                }
                
                _cachesInitialized = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to initialize reflection caches: {e.Message}");
            }
        }
        
        /// <summary>
        /// LateUpdateトランスパイラー - 最適化されたLateUpdateロジックを適用
        /// </summary>
        public static IEnumerable<CodeInstruction> LateUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            try
            {
                // 元のメソッドを完全に置き換える
                var newInstructions = new List<CodeInstruction>();
                
                // 最適化されたLateUpdateメソッドの呼び出しに置き換え
                newInstructions.Add(new CodeInstruction(OpCodes.Ldarg_0)); // this
                newInstructions.Add(new CodeInstruction(OpCodes.Call, 
                    typeof(RenderManagerOptimization).GetMethod(nameof(OptimizedLateUpdate), 
                    BindingFlags.Public | BindingFlags.Static)));
                newInstructions.Add(new CodeInstruction(OpCodes.Ret));
                
                return newInstructions;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} LateUpdate transpiler error: {e.Message}");
                return instructions; // エラー時は元のコードを返す
            }
        }
        
        /// <summary>
        /// 最適化されたLateUpdateメソッド（元のソースコード完全復元版）
        /// </summary>
        public static void OptimizedLateUpdate(object renderManagerInstance)
        {
            try
            {
                // RenderManagerインスタンスの有効性チェック
                if (renderManagerInstance == null)
                {
                    return;
                }
                
                // 1. m_currentFrame += 1U;
                if (_currentFrameField != null)
                {
                    uint currentFrame = (uint)_currentFrameField.GetValue(renderManagerInstance);
                    _currentFrameField.SetValue(renderManagerInstance, currentFrame + 1U);
                }
                
                // 2. m_outOfInstances = false;
                _outOfInstancesField?.SetValue(renderManagerInstance, false);
                
                // 3. PrefabPool.m_canCreateInstances = 1;
                if (_canCreateInstancesField != null)
                {
                    try
                    {
                        _canCreateInstancesField.SetValue(null, 1);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} PrefabPool.m_canCreateInstances access failed: {e.Message}");
                    }
                }
                
                // 4. this.m_lightSystem.m_lightBuffer.Clear();
                if (_lightSystemField != null)
                {
                    var lightSystem = _lightSystemField.GetValue(renderManagerInstance);
                    if (lightSystem != null)
                    {
                        var lightBufferField = lightSystem.GetType().GetField("m_lightBuffer", BindingFlags.Public | BindingFlags.Instance);
                        if (lightBufferField != null)
                        {
                            var lightBuffer = lightBufferField.GetValue(lightSystem);
                            if (lightBuffer != null)
                            {
                                var clearMethod = lightBuffer.GetType().GetMethod("Clear");
                                clearMethod?.Invoke(lightBuffer, null);
                            }
                        }
                    }
                }
                
                // 5. this.m_overlayBuffer.Clear();
                if (_overlayBufferField != null)
                {
                    var overlayBuffer = _overlayBufferField.GetValue(renderManagerInstance);
                    if (overlayBuffer != null)
                    {
                        var clearMethod = overlayBuffer.GetType().GetMethod("Clear");
                        clearMethod?.Invoke(overlayBuffer, null);
                    }
                }
                
                // 6. Singleton<InfoManager>.instance.UpdateInfoMode();
                if (_infoManagerInstanceProperty != null && _updateInfoModeMethod != null)
                {
                    try
                    {
                        var infoManagerInstance = _infoManagerInstanceProperty.GetValue(null, null);
                        if (infoManagerInstance != null)
                        {
                            _updateInfoModeMethod.Invoke(infoManagerInstance, null);
                        }
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} InfoManager.UpdateInfoMode() failed: {e.Message}");
                    }
                }
                
                // 7. if (!Singleton<LoadingManager>.instance.m_loadingComplete) return;
                if (_loadingManagerInstanceProperty != null && _loadingCompleteField != null)
                {
                    try
                    {
                        var loadingManagerInstance = _loadingManagerInstanceProperty.GetValue(null, null);
                        if (loadingManagerInstance != null)
                        {
                            var isLoadingComplete = (bool)_loadingCompleteField.GetValue(loadingManagerInstance);
                            if (!isLoadingComplete) 
                            {
                                return; // ここでロード未完了なら処理終了
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} LoadingManager check failed: {e.Message}");
                        return; // エラーが発生した場合も処理終了
                    }
                }
                
                // 元のソースコード: this.UpdateCameraInfo();
                if (_updateCameraInfoMethod != null)
                {
                    try
                    {
                        _updateCameraInfoMethod.Invoke(renderManagerInstance, null);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} UpdateCameraInfo failed: {e.Message}");
                        // エラーが発生しても処理は続行
                    }
                }
                
                // 元のソースコード: this.UpdateColorMap();
                if (_updateColorMapMethod != null)
                {
                    _updateColorMapMethod.Invoke(renderManagerInstance, null);
                }
                
                // === 元のLateUpdateの完全復元 ===
                
                // 元のソースコード: for (int i = 0; i < RenderManager.m_renderables.m_size; i++) { RenderManager.m_renderables.m_buffer[i].BeginRendering(this.m_cameraInfo); }
                var cameraInfo = _cameraInfoField?.GetValue(renderManagerInstance);
                
                // Get m_renderables static field
                var renderables = _renderablesField?.GetValue(null);
                var renderablesSize = 0;
                var renderablesBuffer = (Array)null;
                
                if (renderables != null)
                {
                    // キャッシュされたフィールドを使用（高速化）
                    if (_renderablesSizeField != null && _renderablesBufferField != null)
                    {
                        renderablesSize = (int)(_renderablesSizeField.GetValue(renderables) ?? 0);
                        renderablesBuffer = _renderablesBufferField.GetValue(renderables) as Array;
                    }
                    else
                    {
                        // フォールバック：リフレクション（低速）
                        var renderablesSizeProperty = renderables.GetType().GetField("m_size");
                        var renderablesBufferProperty = renderables.GetType().GetField("m_buffer");
                        renderablesSize = (int)(renderablesSizeProperty?.GetValue(renderables) ?? 0);
                        renderablesBuffer = renderablesBufferProperty?.GetValue(renderables) as Array;
                    }
                }
                
                try
                {
                    // 最優先最適化：キャッシュされたメソッドを使用
                    if (_beginRenderingMethod != null)
                    {
                        for (int i = 0; i < renderablesSize; i++)
                        {
                            var renderable = renderablesBuffer.GetValue(i);
                            if (renderable != null)
                            {
                                _beginRenderingMethod.Invoke(renderable, new object[] { cameraInfo });
                            }
                        }
                    }
                    else
                    {
                        // フォールバック：リフレクション（低速）
                        for (int i = 0; i < renderablesSize; i++)
                        {
                            var renderable = renderablesBuffer.GetValue(i);
                            var beginRenderingMethod = renderable?.GetType().GetMethod("BeginRendering");
                            beginRenderingMethod?.Invoke(renderable, new object[] { cameraInfo });
                        }
                    }
                }
                finally
                {
                }
                
                try
                {
                    // 元のソースコード: Vector3 min = this.m_cameraInfo.m_bounds.min;
                    // 元のソースコード: Vector3 max = this.m_cameraInfo.m_bounds.max;
                    if (cameraInfo == null)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} cameraInfo is null");
                        return;
                    }
                    
                    // キャッシュされたフィールドを使用（高速化）
                    var bounds = (_cameraInfoBoundsField != null) ? _cameraInfoBoundsField.GetValue(cameraInfo) : cameraInfo.GetType().GetField("m_bounds")?.GetValue(cameraInfo);
                    if (bounds == null)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} bounds is null");
                        return;
                    }
                    
                    var min = (_boundsMinProperty != null) ? (Vector3)_boundsMinProperty.GetValue(bounds, null) : (Vector3)bounds.GetType().GetProperty("min")?.GetValue(bounds, null);
                    var max = (_boundsMaxProperty != null) ? (Vector3)_boundsMaxProperty.GetValue(bounds, null) : (Vector3)bounds.GetType().GetProperty("max")?.GetValue(bounds, null);
                    
                    // シャドウオフセット処理（キャッシュ使用）
                    var shadowOffset = (_cameraInfoShadowOffsetField != null) ? 
                        (Vector3)_cameraInfoShadowOffsetField.GetValue(cameraInfo) : 
                        (Vector3)cameraInfo.GetType().GetField("m_shadowOffset")?.GetValue(cameraInfo);
                    
                    if (shadowOffset.x < 0f)
                    {
                        max.x -= shadowOffset.x;
                    }
                    else
                    {
                        min.x -= shadowOffset.x;
                    }
                    if (shadowOffset.z < 0f)
                    {
                        max.z -= shadowOffset.z;
                    }
                    else
                    {
                        min.z -= shadowOffset.z;
                    }
                    
                    // 元のコードの完全復元
                    int num = Mathf.Max((int)((min.x - 128f) / 384f + 22.5f), 0);
                    int num2 = Mathf.Max((int)((min.z - 128f) / 384f + 22.5f), 0);
                    int num3 = Mathf.Min((int)((max.x + 128f) / 384f + 22.5f), 44);
                    int num4 = Mathf.Min((int)((max.z + 128f) / 384f + 22.5f), 44);
                    int num5 = 5;
                    int num6 = 10000;
                    int num7 = 10000;
                    int num8 = -10000;
                    int num9 = -10000;
                    
                    // 元のソースコード: this.m_renderedGroups.Clear();
                    if (_renderedGroupsField == null)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} m_renderedGroups field not found");
                        return;
                    }
                    var renderedGroups = _renderedGroupsField.GetValue(renderManagerInstance);
                    if (renderedGroups == null)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} renderedGroups is null");
                        return;
                    }
                    if (_clearMethod != null)
                    {
                        _clearMethod.Invoke(renderedGroups, null);
                    }
                    
                    // 元のグループレンダリングループ
                    if (_groupsField == null || _megaGroupsField == null)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} groups or megaGroups fields not found");
                        return;
                    }
                    var groups = (RenderGroup[])_groupsField.GetValue(renderManagerInstance);
                    var megaGroups = (MegaRenderGroup[])_megaGroupsField.GetValue(renderManagerInstance);
                    if (groups == null || megaGroups == null)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} groups or megaGroups arrays are null");
                        return;
                    }
                    
                    for (int j = num2; j <= num4; j++)
                    {
                        for (int k = num; k <= num3; k++)
                        {
                            int num10 = j * 45 + k;
                            RenderGroup renderGroup = groups[num10];
                            if (renderGroup != null && renderGroup.Render((RenderManager.CameraInfo)cameraInfo))
                            {
                                // 元のソースコード: this.m_renderedGroups.Add(renderGroup);
                                if (_addMethod != null)
                                {
                                    _addMethod.Invoke(renderedGroups, new object[] { renderGroup });
                                }
                                
                                int num11 = k / num5;
                                int num12 = j / num5;
                                int num13 = num12 * 9 + num11;
                                MegaRenderGroup megaRenderGroup = megaGroups[num13];
                                if (megaRenderGroup != null)
                                {
                                    // 元のコードの完全復元
                                    megaRenderGroup.m_layersRendered2 |= (megaRenderGroup.m_layersRendered1 & renderGroup.m_layersRendered);
                                    megaRenderGroup.m_layersRendered1 |= renderGroup.m_layersRendered;
                                    megaRenderGroup.m_instanceMask |= renderGroup.m_instanceMask;
                                    num6 = Mathf.Min(num6, num11);
                                    num7 = Mathf.Min(num7, num12);
                                    num8 = Mathf.Max(num8, num11);
                                    num9 = Mathf.Max(num9, num12);
                                }
                            }
                        }
                    }
                    
                    // 第1段階: MegaRenderGroup.Render()
                    for (int l = num7; l <= num9; l++)
                    {
                        for (int m = num6; m <= num8; m++)
                        {
                            int num14 = l * 9 + m;
                            MegaRenderGroup megaRenderGroup2 = megaGroups[num14];
                            if (megaRenderGroup2 != null)
                            {
                                megaRenderGroup2.Render();
                            }
                        }
                    }
                    
                    // 第2段階: RenderGroup.Render(groupMask) - 元のコードの完全復元
                    if (_sizeField == null || _bufferField == null)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} m_size or m_buffer fields not found on renderedGroups");
                        return;
                    }
                    int renderedGroupsSize = (int)_sizeField.GetValue(renderedGroups);
                    var renderedGroupsBuffer = (RenderGroup[])_bufferField.GetValue(renderedGroups);
                    
                    if (renderedGroupsBuffer == null)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} renderedGroupsBuffer is null");
                        return;
                    }
                    
                    for (int n = 0; n < renderedGroupsSize; n++)
                    {
                        RenderGroup renderGroup2 = renderedGroupsBuffer[n];
                        if (renderGroup2 != null)
                        {
                            int num15 = renderGroup2.m_x / num5;
                            int num16 = renderGroup2.m_z / num5;
                            int num17 = num16 * 9 + num15;
                            MegaRenderGroup megaRenderGroup3 = megaGroups[num17];
                            if (megaRenderGroup3 != null && megaRenderGroup3.m_groupMask != 0)
                            {
                                renderGroup2.Render(megaRenderGroup3.m_groupMask);
                            }
                        }
                    }
                }
                finally
                {
                }
                
                try
                {
                    // 最優先最適化：キャッシュされたメソッドを使用
                    if (_endRenderingMethod != null)
                    {
                        for (int num18 = 0; num18 < renderablesSize; num18++)
                        {
                            var renderable = renderablesBuffer.GetValue(num18);
                            if (renderable != null)
                            {
                                _endRenderingMethod.Invoke(renderable, new object[] { cameraInfo });
                            }
                        }
                    }
                    else
                    {
                        // フォールバック：リフレクション（低速）
                        for (int num18 = 0; num18 < renderablesSize; num18++)
                        {
                            var renderable = renderablesBuffer.GetValue(num18);
                            var endRenderingMethod = renderable?.GetType().GetMethod("EndRendering");
                            endRenderingMethod?.Invoke(renderable, new object[] { cameraInfo });
                        }
                    }
                }
                finally
                {
                }
                
                // 元のソースコード: this.m_lightSystem.EndRendering(this.m_cameraInfo);
                if (_lightSystemField != null && cameraInfo != null)
                {
                    var lightSystem = _lightSystemField.GetValue(renderManagerInstance);
                    if (lightSystem != null)
                    {
                        if (_lightSystemEndRenderingMethod != null)
                        {
                            _lightSystemEndRenderingMethod.Invoke(lightSystem, new object[] { cameraInfo });
                        }
                        else
                        {
                            var endRenderingMethod = lightSystem.GetType().GetMethod("EndRendering");
                            endRenderingMethod?.Invoke(lightSystem, new object[] { cameraInfo });
                        }
                    }
                }
                
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} OptimizedLateUpdate failed: {e.Message}");
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Exception type: {e.GetType().Name}");
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Stack trace: {e.StackTrace}");
                
                if (e.InnerException != null)
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Inner exception: {e.InnerException.Message}");
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Inner exception type: {e.InnerException.GetType().Name}");
                }
            }
        }
    }
}
