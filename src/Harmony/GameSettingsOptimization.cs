using ColossalFramework;
using HarmonyLib;
using System.Threading;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// GameSettings保存間隔を1秒から1分に変更して、頻繁な保存によるパフォーマンス問題を軽減
    /// </summary>
    public class GameSettingsOptimization
    {
        /// <summary>
        /// GameSettings.MonitorSaveメソッドの間隔を1000ms（1秒）から60000ms（1分）に変更
        /// userGameState.cgsファイル（2.6MB）の頻繁な保存を軽減し、79ms/frameのボトルネックを改善
        /// 
        /// 対象コード: Monitor.Wait(GameSettings.m_LockObject, 1000);
        /// 変更後: Monitor.Wait(GameSettings.m_LockObject, 60000);
        /// </summary>
        [HarmonyPatch(typeof(GameSettings), "MonitorSave")]
        public class GameSettings_MonitorSave_Patch
        {
            // Monitor.Wait(m_LockObject, 1000)をMonitor.Wait(m_LockObject, 60000)に置き換え
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                bool found = false;

                for (int i = 0; i < codes.Count; i++)
                {
                    var instruction = codes[i];
                    
                    // Monitor.Wait(m_LockObject, 1000)のパターンを探す
                    if (instruction.opcode == OpCodes.Ldc_I4 && 
                        instruction.operand is int value && value == 1000)
                    {
                        // 次の命令がMonitor.Waitの呼び出しかチェック
                        if (i + 1 < codes.Count && 
                            codes[i + 1].opcode == OpCodes.Call)
                        {
                            var nextInstruction = codes[i + 1];
                            if (nextInstruction.operand is System.Reflection.MethodInfo methodInfo &&
                                methodInfo.DeclaringType == typeof(Monitor) &&
                                methodInfo.Name == "Wait")
                            {
                                // 1000を60000に変更
                                codes[i] = new CodeInstruction(OpCodes.Ldc_I4, 60000);
                                found = true;
                                UnityEngine.Debug.Log($"{CS1Profiler.Core.Constants.LOG_PREFIX} GameSettings MonitorSave: Wait interval changed from 1000ms to 60000ms (1 minute)");
                                break;
                            }
                        }
                    }
                }

                if (!found)
                {
                    UnityEngine.Debug.Log($"{CS1Profiler.Core.Constants.LOG_PREFIX} GameSettings MonitorSave: Monitor.Wait(1000) pattern not found");
                }

                return codes;
            }
        }

        /// <summary>
        /// 代替案：もしTranspilerが動作しない場合、SaveAllメソッドをフック
        /// </summary>
        [HarmonyPatch(typeof(GameSettings), "SaveAll")]
        public class GameSettings_SaveAll_Patch
        {
            private static System.DateTime lastSaveTime = System.DateTime.MinValue;
            private static readonly double SAVE_INTERVAL_MINUTES = 1.0; // 1分間隔

            // SaveAllの実行頢度を制限
            static bool Prefix()
            {
                var now = System.DateTime.Now;
                var timeSinceLastSave = now - lastSaveTime;

                // 1分以内の場合は保存をスキップ
                if (timeSinceLastSave.TotalMinutes < SAVE_INTERVAL_MINUTES)
                {
                    return false; // 元のメソッドを実行しない
                }

                lastSaveTime = now;
                UnityEngine.Debug.Log($"{CS1Profiler.Core.Constants.LOG_PREFIX} GameSettings SaveAll: Allowing save (last save: {timeSinceLastSave.TotalMinutes:F1} minutes ago)");
                return true; // 元のメソッドを実行
            }
        }
    }
}
