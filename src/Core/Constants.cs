using System;

namespace CS1Profiler.Core
{
    /// <summary>
    /// CS1Profiler MOD 共通定数
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// MOD名（将来変更の可能性に対応）
        /// </summary>
        public const string MOD_NAME = "CS1Profiler";
        
        /// <summary>
        /// ログプレフィックス（性能影響なし）
        /// </summary>
        public const string LOG_PREFIX = "[CS1Profiler]";
        
        /// <summary>
        /// Harmonyパッチ識別子
        /// </summary>
        public const string HARMONY_ID = "tokachi269.cs1profiler.startup";
        
        /// <summary>
        /// ProfilerManager GameObject名
        /// </summary>
        public const string PROFILER_MANAGER_NAME = "CS1ProfilerManager";
        
        /// <summary>
        /// Performance Monitor GameObject名
        /// </summary>
        public const string PERFORMANCE_MONITOR_NAME = "CS1ProfilerMonitor";
        
        /// <summary>
        /// 型名フィルタープレフィックス（Harmonyパッチ除外用）
        /// </summary>
        public const string TYPE_FILTER_PREFIX = "CS1Profiler";
        
        /// <summary>
        /// アセンブリタイトル（Assembly属性で使用可能）
        /// </summary>
        public const string ASSEMBLY_TITLE = "CS1Profiler";
        
        /// <summary>
        /// アセンブリバージョン（Assembly属性で使用可能）
        /// </summary>
        public const string ASSEMBLY_VERSION = "1.0.0.0";
    }
}
