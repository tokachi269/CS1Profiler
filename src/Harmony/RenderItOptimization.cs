using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ColossalFramework.Plugins;

namespace CS1Profiler
{
    /// <summary>
    /// RenderItのModUtils最適化パッチ（統合版）
    /// </summary>
    internal static class RenderItOptimization
    {
        public static void ApplyRenderItOptimizationPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                // RenderItがロードされているかチェック
                bool renderItFound = false;
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "RenderIt")
                    {
                        renderItFound = true;
                        break;
                    }
                }

                if (renderItFound)
                {
                    ApplyRenderItPatches(harmony);
                }
                else
                {
                    UnityEngine.Debug.Log("[CS1Profiler] RenderIt not detected, optimization patches skipped");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] RenderIt optimization patches error: " + e.Message);
            }
        }

        private static void ApplyRenderItPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                var renderItAsm = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RenderIt");
                if (renderItAsm != null)
                {
                    var modUtilsType = renderItAsm.GetType("RenderIt.ModUtils");
                    if (modUtilsType != null)
                    {
                        var isModEnabledMethod = modUtilsType.GetMethod("IsModEnabled", new Type[] { typeof(string) });
                        var isAnyModsEnabledMethod = modUtilsType.GetMethod("IsAnyModsEnabled", new Type[] { typeof(string[]) });
                        
                        if (isModEnabledMethod != null)
                        {
                            var prefixMethod = typeof(RenderItOptimizationHooks).GetMethod("OptimizedIsModEnabled", BindingFlags.Static | BindingFlags.Public);
                            if (prefixMethod != null)
                            {
                                harmony.Patch(isModEnabledMethod, new HarmonyMethod(prefixMethod));
                            }
                        }
                        
                        if (isAnyModsEnabledMethod != null)
                        {
                            var prefixMethod2 = typeof(RenderItOptimizationHooks).GetMethod("OptimizedIsAnyModsEnabled", BindingFlags.Static | BindingFlags.Public);
                            if (prefixMethod2 != null)
                            {
                                harmony.Patch(isAnyModsEnabledMethod, new HarmonyMethod(prefixMethod2));
                            }
                        }
                        
                        UnityEngine.Debug.Log("[CS1Profiler] RenderIt optimization patches applied successfully");
                    }
                }
            }
            catch (Exception patchEx)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] RenderIt patch application error: " + patchEx.Message);
            }
        }
    }

    /// <summary>
    /// RenderItのModUtils最適化Hook実装
    /// </summary>
    internal static class RenderItOptimizationHooks
    {
        private static readonly Dictionary<string, bool> _modCache = new Dictionary<string, bool>();
        private static bool _cacheInitialized = false;

        public static bool OptimizedIsModEnabled(string name, ref bool __result)
        {
            try
            {
                // 初回のみキャッシュを構築
                if (!_cacheInitialized)
                {
                    BuildModCache();
                    _cacheInitialized = true;
                }

                // キャッシュから結果を返す
                __result = _modCache.TryGetValue(name.ToLower(), out bool isEnabled) && isEnabled;
                return false; // 元のメソッドを実行しない
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] OptimizedIsModEnabled error: " + e.Message);
                return true; // エラー時は元のメソッドを実行
            }
        }

        public static bool OptimizedIsAnyModsEnabled(string[] names, ref bool __result)
        {
            try
            {
                if (!_cacheInitialized)
                {
                    BuildModCache();
                    _cacheInitialized = true;
                }

                // 配列を直接チェック（早期リターン）
                foreach (string name in names)
                {
                    if (_modCache.TryGetValue(name.ToLower(), out bool isEnabled) && isEnabled)
                    {
                        __result = true;
                        return false;
                    }
                }

                __result = false;
                return false; // 元のメソッドを実行しない
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] OptimizedIsAnyModsEnabled error: " + e.Message);
                return true; // エラー時は元のメソッドを実行
            }
        }

        private static void BuildModCache()
        {
            try
            {
                _modCache.Clear();
                
                foreach (PluginManager.PluginInfo plugin in PluginManager.instance.GetPluginsInfo())
                {
                    foreach (Assembly assembly in plugin.GetAssemblies())
                    {
                        string assemblyName = assembly.GetName().Name.ToLower();
                        if (!_modCache.ContainsKey(assemblyName))
                        {
                            _modCache[assemblyName] = plugin.isEnabled;
                        }
                    }
                }
                
                UnityEngine.Debug.Log($"[CS1Profiler] RenderIt ModUtils cache built: {_modCache.Count} entries");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] BuildModCache error: " + e.Message);
            }
        }

        public static void ClearCache()
        {
            _modCache.Clear();
            _cacheInitialized = false;
            UnityEngine.Debug.Log("[CS1Profiler] RenderIt ModUtils cache cleared");
        }
    }
}
