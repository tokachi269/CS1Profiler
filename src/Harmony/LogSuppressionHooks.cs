using HarmonyLib;
using UnityEngine;

namespace CS1Profiler
{
    /// <summary>
    /// ログ抑制用のHarmonyパッチ
    /// </summary>
    public static class LogSuppressionHooks
    {
        public static bool SuppressPackageDeserializerLogs { get; set; } = true;

        // PackageDeserializerのLogWarningメソッドを抑制
        [HarmonyPatch(typeof(ColossalFramework.Packaging.PackageDeserializer), "LogWarning")]
        [HarmonyPrefix]
        public static bool SuppressPackageDeserializerWarning(string message)
        {
            if (!SuppressPackageDeserializerLogs) return true; // 通す
            return false; // 抑制
        }

        // PackageManagerのLogWarningメソッドも抑制
        [HarmonyPatch(typeof(ColossalFramework.Packaging.PackageManager), "LogWarning")]
        [HarmonyPrefix]
        public static bool SuppressPackageManagerWarning(string message)
        {
            if (!SuppressPackageDeserializerLogs) return true; // 通す
            return false; // 抑制
        }

        // 初期化時にパッチ状態を確認
        public static void Initialize()
        {
            UnityEngine.Debug.Log($"[CS1Profiler] LogSuppressionHooks initialized - SuppressLogs: {SuppressPackageDeserializerLogs}");
        }
    }
}
