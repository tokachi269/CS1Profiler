using HarmonyLib;
using ICities;
using System;
using System.Reflection;
using UnityEngine;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// 起動時解析用のHarmonyパッチ管理
    /// BootStrapper、PackageManager、LoadingExtensionの重要メソッドを監視
    /// </summary>
    public static class StartupAnalysisPatcher
    {
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Patching startup critical methods...");
                
                // BootStrapper.Boot のパッチ（重要な起動ボトルネック）
                PatchBootStrapper(harmony);
                
                // PackageManager.Ensure のパッチ（MOD読み込みの遅延原因）
                PatchPackageManager(harmony);
                
                // LoadingExtensionBaseの主要メソッドをパッチ
                PatchLoadingExtensions(harmony);

                UnityEngine.Debug.Log("[CS1Profiler] Startup critical methods patching completed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Startup methods patch failed: " + e.Message);
            }
        }

        private static void PatchBootStrapper(HarmonyLib.Harmony harmony)
        {
            try
            {
                var bootStrapperType = Type.GetType("ColossalFramework.BootStrapper, ColossalManaged");
                if (bootStrapperType != null)
                {
                    // Boot() メソッドをパッチ
                    var bootMethods = bootStrapperType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    foreach (var method in bootMethods)
                    {
                        if (method.Name == "Boot")
                        {
                            harmony.Patch(method,
                                prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "BootStrapper_Boot_Pre"),
                                postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "BootStrapper_Boot_Post"));
                            UnityEngine.Debug.Log("[CS1Profiler] BootStrapper.Boot patched: " + method.GetParameters().Length + " parameters");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] BootStrapper patch failed: " + e.Message);
            }
        }

        private static void PatchPackageManager(HarmonyLib.Harmony harmony)
        {
            try
            {
                var packageManagerType = Type.GetType("ColossalFramework.Packaging.PackageManager, ColossalManaged");
                if (packageManagerType != null)
                {
                    var ensureMethod = packageManagerType.GetMethod("Ensure", BindingFlags.Public | BindingFlags.Static);
                    if (ensureMethod != null)
                    {
                        harmony.Patch(ensureMethod,
                            prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "PackageManager_Ensure_Pre"),
                            postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "PackageManager_Ensure_Post"));
                        UnityEngine.Debug.Log("[CS1Profiler] PackageManager.Ensure patched successfully");
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PackageManager patch failed: " + e.Message);
            }
        }

        private static void PatchLoadingExtensions(HarmonyLib.Harmony harmony)
        {
            try
            {
                // LoadingExtensionBaseの重要なメソッドをパッチして詳細な起動解析を実行
                var extensionType = typeof(LoadingExtensionBase);
                
                var onCreatedMethod = extensionType.GetMethod("OnCreated", BindingFlags.Public | BindingFlags.Instance);
                if (onCreatedMethod != null)
                {
                    harmony.Patch(onCreatedMethod,
                        prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnCreated_Pre"),
                        postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnCreated_Post"));
                    UnityEngine.Debug.Log("[CS1Profiler] LoadingExtensionBase.OnCreated patched");
                }

                var onLevelLoadedMethod = extensionType.GetMethod("OnLevelLoaded", BindingFlags.Public | BindingFlags.Instance);
                if (onLevelLoadedMethod != null)
                {
                    harmony.Patch(onLevelLoadedMethod,
                        prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnLevelLoaded_Pre"),
                        postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnLevelLoaded_Post"));
                    UnityEngine.Debug.Log("[CS1Profiler] LoadingExtensionBase.OnLevelLoaded patched");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension patch failed: " + e.Message);
            }
        }
    }

    /// <summary>
    /// 起動時の重要な処理をフック・分析するクラス
    /// </summary>
    public static class StartupHooks
    {
        private static DateTime _hookStartTime;
        
        // BootStrapper.Boot() のフック
        public static void BootStrapper_Boot_Pre()
        {
            try
            {
                _hookStartTime = DateTime.Now;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] BootStrapper.Boot() - PRE");
                LogStartupEvent("BOOTSTRAPPER_BOOT_START", "BootStrapper.Boot() method started");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] BootStrapper_Boot_Pre error: " + e.Message);
            }
        }

        public static void BootStrapper_Boot_Post()
        {
            try
            {
                TimeSpan elapsed = DateTime.Now - _hookStartTime;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] BootStrapper.Boot() - POST (" + elapsed.TotalMilliseconds.ToString("F1") + "ms)");
                LogStartupEvent("BOOTSTRAPPER_BOOT_END", "BootStrapper.Boot() completed in " + elapsed.TotalMilliseconds.ToString("F1") + "ms");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] BootStrapper_Boot_Post error: " + e.Message);
            }
        }

        // PackageManager.Ensure() のフック（MOD読み込みのボトルネック）
        public static void PackageManager_Ensure_Pre()
        {
            try
            {
                _hookStartTime = DateTime.Now;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] PackageManager.Ensure() - PRE");
                LogStartupEvent("PACKAGEMANAGER_ENSURE_START", "PackageManager.Ensure() started - MOD loading phase");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PackageManager_Ensure_Pre error: " + e.Message);
            }
        }

        public static void PackageManager_Ensure_Post()
        {
            try
            {
                TimeSpan elapsed = DateTime.Now - _hookStartTime;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] PackageManager.Ensure() - POST (" + elapsed.TotalMilliseconds.ToString("F1") + "ms)");
                LogStartupEvent("PACKAGEMANAGER_ENSURE_END", "PackageManager.Ensure() completed in " + elapsed.TotalMilliseconds.ToString("F1") + "ms - MOD loading finished");
                
                // 重要：MOD読み込み完了時のメモリ使用量も記録（ProfilerManager経由）
                if (CS1Profiler.Managers.ProfilerManager.Instance != null && CS1Profiler.Managers.ProfilerManager.Instance.CsvManager != null)
                {
                    long memoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                    CS1Profiler.Managers.ProfilerManager.Instance.CsvManager.QueueCsvWrite("Memory", "PostMODLoad", memoryMB, 1, elapsed.TotalMilliseconds, 0, "Memory usage after MOD loading");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PackageManager_Ensure_Post error: " + e.Message);
            }
        }

        // LoadingExtension.OnCreated() のフック
        public static void LoadingExtension_OnCreated_Pre(ICities.LoadingExtensionBase __instance)
        {
            try
            {
                _hookStartTime = DateTime.Now;
                string typeName = __instance.GetType().Name;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] " + typeName + ".OnCreated() - PRE");
                LogStartupEvent("LOADING_ONCREATED_START", typeName + ".OnCreated() started");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension_OnCreated_Pre error: " + e.Message);
            }
        }

        public static void LoadingExtension_OnCreated_Post(ICities.LoadingExtensionBase __instance)
        {
            try
            {
                TimeSpan elapsed = DateTime.Now - _hookStartTime;
                string typeName = __instance.GetType().Name;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] " + typeName + ".OnCreated() - POST (" + elapsed.TotalMilliseconds.ToString("F1") + "ms)");
                LogStartupEvent("LOADING_ONCREATED_END", typeName + ".OnCreated() completed in " + elapsed.TotalMilliseconds.ToString("F1") + "ms");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension_OnCreated_Post error: " + e.Message);
            }
        }

        // LoadingExtension.OnLevelLoaded() のフック
        public static void LoadingExtension_OnLevelLoaded_Pre(ICities.LoadingExtensionBase __instance, ICities.LoadMode mode)
        {
            try
            {
                _hookStartTime = DateTime.Now;
                string typeName = __instance.GetType().Name;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] " + typeName + ".OnLevelLoaded(" + mode.ToString() + ") - PRE");
                LogStartupEvent("LOADING_ONLEVELLOAD_START", typeName + ".OnLevelLoaded(" + mode.ToString() + ") started");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension_OnLevelLoaded_Pre error: " + e.Message);
            }
        }

        public static void LoadingExtension_OnLevelLoaded_Post(LoadingExtensionBase __instance, LoadMode mode)
        {
            try
            {
                TimeSpan elapsed = DateTime.Now - _hookStartTime;
                string typeName = __instance.GetType().Name;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] " + typeName + ".OnLevelLoaded(" + mode.ToString() + ") - POST (" + elapsed.TotalMilliseconds.ToString("F1") + "ms)");
                LogStartupEvent("LOADING_ONLEVELLOAD_END", typeName + ".OnLevelLoaded(" + mode.ToString() + ") completed in " + elapsed.TotalMilliseconds.ToString("F1") + "ms");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension_OnLevelLoaded_Post error: " + e.Message);
            }
        }

        // 共通の起動イベントログ機能（静的アクセス）
        private static void LogStartupEvent(string eventType, string description)
        {
            try
            {
                // Modクラスの静的LogStartupEventメソッドを呼び出し
                // リフレクションを使用して安全に呼び出し
                var modType = typeof(Mod);
                var method = modType.GetMethod("LogStartupEvent", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    method.Invoke(null, new object[] { eventType, description });
                }
                else
                {
                    // フォールバック：直接ログ出力
                    UnityEngine.Debug.Log("[CS1Profiler-Startup-Fallback] " + eventType + " | " + description);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] StartupHooks.LogStartupEvent error: " + e.Message);
            }
        }
    }
}
