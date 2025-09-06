using System.Diagnostics;
using System.Reflection;
using CS1Profiler.Harmony;
using CS1Profiler.Core;

namespace CS1Profiler.Profiling
{
    /// <summary>
    /// 超軽量パフォーマンスフック
    /// Producer側：Stopwatch.GetTimestamp()のみ（影響ほぼ0）
    /// メソッド名は事前文字列化でキャッシュ
    /// </summary>
    public static class LightweightPerformanceHooks
    {
        /// <summary>
        /// 超軽量プレフィックス（計測開始）
        /// 返り値: タイムスタンプ（long）
        /// </summary>
        public static void ProfilerPrefix(MethodBase __originalMethod, out long __state)
        {
            // PatchController.PerformanceProfilingEnabled の状態チェック（高速）
            if (!PatchController.PerformanceProfilingEnabled)
            {
                __state = 0;
                return; // 即座にリターン（分岐オーバーヘッドのみ）
            }
            
            // Stopwatch.GetTimestamp()のみ（最高精度、最軽量）
            __state = Stopwatch.GetTimestamp();
        }
        
        /// <summary>
        /// 超軽量ポストフィックス（計測終了→エンキュー）
        /// メソッド名文字列化はConsumer側で実行
        /// </summary>
        public static void ProfilerPostfix(MethodBase __originalMethod, long __state)
        {
            // 早期リターン（無効状態またはstartTicks=0）
            if (__state == 0 || !PatchController.PerformanceProfilingEnabled)
                return;
            
            // 終了時刻取得（高精度）
            long endTicks = Stopwatch.GetTimestamp();
            
            // 💡重要最適化：文字列化をConsumer側に移譲
            // Producer側ではMethodBaseの型情報のみ渡す
            // （文字列化・反射処理は絶対しない）
            MPSCLogger.EnqueueMethod(__originalMethod, __state, endTicks);
        }
    }
}
