using HarmonyLib;
using System;
using UnityEngine;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// ログ出力を抑制するHarmonyパッチ
    /// </summary>
    public static class LogSuppressionHooks
    {
        public static bool SuppressPackageDeserializerLogs { get; set; } = true; // デフォルトON

        // PackageDeserializerのログを抑制
        [HarmonyPatch(typeof(ColossalFramework.Packaging.PackageDeserializer), "LogWarning")]
        [HarmonyPrefix]
        public static bool SuppressPackageWarning(string message)
        {
            if (SuppressPackageDeserializerLogs)
            {
                UnityEngine.Debug.Log("[CS1Profiler] Suppressed PackageDeserializer warning: " + message);
                return false; // ログを抑制
            }
            return true; // 通常通りログ出力
        }

        // PackageManagerのログも抑制
        [HarmonyPatch(typeof(ColossalFramework.Packaging.PackageManager), "LogWarning")]
        [HarmonyPrefix] 
        public static bool SuppressPackageManagerWarning(string message)
        {
            if (SuppressPackageDeserializerLogs)
            {
                UnityEngine.Debug.Log("[CS1Profiler] Suppressed PackageManager warning: " + message);
                return false;
            }
            return true;
        }

        // 初期化時にパッチ状態を確認
        public static void Initialize()
        {
            UnityEngine.Debug.Log("[CS1Profiler] LogSuppressionHooks initialized");
            UnityEngine.Debug.Log($"[CS1Profiler] SuppressPackageDeserializerLogs: {SuppressPackageDeserializerLogs}");
        }
    }
}
