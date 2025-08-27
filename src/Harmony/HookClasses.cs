using HarmonyLib;
using ICities;
using System;
using System.Reflection;
using UnityEngine;

namespace CS1Profiler
{
    /// <summary>
    /// 起動時解析用のHooks
    /// </summary>
    internal static class StartupHooks
    {
        // BootStrapper.Boot Pre/Post
        public static void BootStrapper_Boot_Pre()
        {
            Mod.LogStartupEvent("BOOTSTRAP_START", "BootStrapper.Boot() started");
        }

        public static void BootStrapper_Boot_Post()
        {
            Mod.LogStartupEvent("BOOTSTRAP_END", "BootStrapper.Boot() completed");
        }

        // PackageManager.Ensure Pre/Post
        public static void PackageManager_Ensure_Pre(string name)
        {
            Mod.LogStartupEvent("PACKAGE_ENSURE_START", $"PackageManager.Ensure({name}) started");
        }

        public static void PackageManager_Ensure_Post(string name)
        {
            Mod.LogStartupEvent("PACKAGE_ENSURE_END", $"PackageManager.Ensure({name}) completed");
        }

        // LoadingExtension.OnCreated Pre/Post
        public static void LoadingExtension_OnCreated_Pre(LoadingExtensionBase __instance, ILoading loading)
        {
            Mod.LogStartupEvent("EXTENSION_CREATED_START", $"LoadingExtension.OnCreated({__instance.GetType().Name}) started");
        }

        public static void LoadingExtension_OnCreated_Post(LoadingExtensionBase __instance, ILoading loading)
        {
            Mod.LogStartupEvent("EXTENSION_CREATED_END", $"LoadingExtension.OnCreated({__instance.GetType().Name}) completed");
        }

        // LoadingExtension.OnLevelLoaded Pre/Post
        public static void LoadingExtension_OnLevelLoaded_Pre(LoadingExtensionBase __instance, LoadMode mode)
        {
            Mod.LogStartupEvent("EXTENSION_LEVEL_LOADED_START", $"LoadingExtension.OnLevelLoaded({__instance.GetType().Name}, {mode}) started");
        }

        public static void LoadingExtension_OnLevelLoaded_Post(LoadingExtensionBase __instance, LoadMode mode)
        {
            Mod.LogStartupEvent("EXTENSION_LEVEL_LOADED_END", $"LoadingExtension.OnLevelLoaded({__instance.GetType().Name}, {mode}) completed");
        }
    }
}
