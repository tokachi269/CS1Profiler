using HarmonyLib;
using ICities;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CS1Profiler
{
    /// <summary>
    /// 起動時解析用のパッチ
    /// </summary>
    internal static class StartupPatches
    {
        public static void PatchStartupMethods(HarmonyLib.Harmony harmony)
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Patching startup methods...");

                // ログ抑制パッチ
                PatchPackageDeserializerLogs(harmony);
                
                // 起動重要メソッドパッチ
                PatchBootstrapMethods(harmony);
                PatchPackageManagerMethods(harmony);
                PatchLoadingExtensionMethods(harmony);
                
                UnityEngine.Debug.Log("[CS1Profiler] Startup methods patching completed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PatchStartupMethods error: " + e.Message);
            }
        }

        private static void PatchPackageDeserializerLogs(HarmonyLib.Harmony harmony)
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Attempting to patch PackageDeserializer logs...");
                
                // LogSuppressionHooksを初期化
                LogSuppressionHooks.Initialize();
                
                // このクラスの HarmonyPatch を適用
                harmony.CreateClassProcessor(typeof(LogSuppressionHooks)).Patch();
                
                UnityEngine.Debug.Log("[CS1Profiler] LogSuppressionHooks patches applied successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Log suppression patches error: " + e.Message);
            }
        }

        private static void PatchBootstrapMethods(HarmonyLib.Harmony harmony)
        {
            try
            {
                // ColossalFramework.BootStrapper.Boot パッチ
                var bootStrapperType = Type.GetType("ColossalFramework.BootStrapper, ColossalManaged");
                if (bootStrapperType != null)
                {
                    var bootMethod = bootStrapperType.GetMethod("Boot", BindingFlags.Public | BindingFlags.Static);
                    if (bootMethod != null)
                    {
                        harmony.Patch(bootMethod,
                            prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "BootStrapper_Boot_Pre"),
                            postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "BootStrapper_Boot_Post"));
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Bootstrap patches error: " + e.Message);
            }
        }

        private static void PatchPackageManagerMethods(HarmonyLib.Harmony harmony)
        {
            try
            {
                // ColossalFramework.Packaging.PackageManager.Ensure パッチ
                var packageManagerType = Type.GetType("ColossalFramework.Packaging.PackageManager, ColossalManaged");
                if (packageManagerType != null)
                {
                    var ensureMethod = packageManagerType.GetMethod("Ensure", new Type[] { typeof(string) });
                    if (ensureMethod != null)
                    {
                        harmony.Patch(ensureMethod,
                            prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "PackageManager_Ensure_Pre"),
                            postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "PackageManager_Ensure_Post"));
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PackageManager patches error: " + e.Message);
            }
        }

        private static void PatchLoadingExtensionMethods(HarmonyLib.Harmony harmony)
        {
            try
            {
                // LoadingExtensionBase.OnCreated パッチ
                var extensionType = typeof(LoadingExtensionBase);
                var onCreatedMethod = extensionType.GetMethod("OnCreated", new Type[] { typeof(ILoading) });
                if (onCreatedMethod != null)
                {
                    harmony.Patch(onCreatedMethod,
                        prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnCreated_Pre"),
                        postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnCreated_Post"));
                }

                var onLevelLoadedMethod = extensionType.GetMethod("OnLevelLoaded", new Type[] { typeof(LoadMode) });
                if (onLevelLoadedMethod != null)
                {
                    harmony.Patch(onLevelLoadedMethod,
                        prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnLevelLoaded_Pre"),
                        postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnLevelLoaded_Post"));
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension patches error: " + e.Message);
            }
        }
    }
}
