using ColossalFramework;
using ColossalFramework.Plugins;
using HarmonyLib;
using ICities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using CS1Profiler.Managers;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// Harmonyパッチの制御クラス
    /// </summary>
    public static class Hooks
    {
        private static HarmonyLib.Harmony harmonyInstance;
        private static bool isEnabled = false;
        
        public static void Initialize()
        {
            try
            {
                if (harmonyInstance == null)
                {
                    harmonyInstance = new HarmonyLib.Harmony("CS1Profiler.Harmony");
                    harmonyInstance.PatchAll();
                    isEnabled = true;
                    UnityEngine.Debug.Log("[CS1Profiler] Harmony hooks initialized");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Hooks.Initialize error: " + e.Message);
            }
        }
        
        public static void Cleanup()
        {
            try
            {
                if (harmonyInstance != null)
                {
                    harmonyInstance.UnpatchAll();
                    harmonyInstance = null;
                    isEnabled = false;
                    UnityEngine.Debug.Log("[CS1Profiler] Harmony hooks cleaned up");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Hooks.Cleanup error: " + e.Message);
            }
        }
        
        public static bool IsEnabled()
        {
            return isEnabled;
        }

        // プロファイリング制御用のメソッド
        public static void Pre(MethodBase __originalMethod)
        {
            if (!isEnabled) return;
            
            try
            {
                CS1Profiler.Profiling.MethodProfiler.MethodStart(__originalMethod);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Pre error: " + e.Message);
            }
        }

        public static void Post(MethodBase __originalMethod)
        {
            if (!isEnabled) return;
            
            try
            {
                CS1Profiler.Profiling.MethodProfiler.MethodEnd(__originalMethod);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Post error: " + e.Message);
            }
        }
    }

    public static class Patcher
    {
        private const string HarmonyId = "CS1Profiler.Harmony";
        private static bool patched = false;

        public static void PatchAll()
        {
            UnityEngine.Debug.Log("[CS1Profiler] Harmony PatchAll starting...");
            
            if (patched) 
            {
                UnityEngine.Debug.Log("[CS1Profiler] Already patched, skipping");
                return;
            }

            try
            {
                patched = true;
                var harmony = new HarmonyLib.Harmony(HarmonyId);
                
                // Harmonyの自動パッチ適用（[HarmonyPatch]アトリビュート付きクラスを検索）
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                
                // MethodProfilerの初期化
                CS1Profiler.Profiling.MethodProfiler.Initialize(harmony);
                
                // LogSuppressionHooksの初期化
                LogSuppressionHooks.Initialize();
                
                UnityEngine.Debug.Log("[CS1Profiler] Harmony patching completed successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PatchAll error: " + e.Message);
                UnityEngine.Debug.LogError("[CS1Profiler] Stack trace: " + e.StackTrace);
                patched = false;
            }
        }

        public static void UnpatchAll()
        {
            if (!patched) return;

            try
            {
                var harmony = new HarmonyLib.Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
                patched = false;
                UnityEngine.Debug.Log("[CS1Profiler] All patches removed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] UnpatchAll error: " + e.Message);
            }
        }
    }
}
