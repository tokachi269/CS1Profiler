using System;
using System.Globalization;
using CS1Profiler.Core;

namespace CS1Profiler.UI
{
    /// <summary>
    /// 多言語対応文字列リソース
    /// 設定UI用のツールチップテキスト管理
    /// TranslationFrameworkを使用した多言語対応
    /// </summary>
    public static class LocalizedStrings
    {
        /// <summary>
        /// 翻訳キーを使用して文字列を取得
        /// </summary>
        /// <param name="key">翻訳キー</param>
        /// <returns>翻訳された文字列（キーが見つからない場合は英語フォールバック）</returns>
        private static string GetString(string key)
        {
            try
            {
                // TranslationFrameworkを使用して翻訳を取得
                return FindIt.Translations.Translate(key);
            }
            catch
            {
                // フォールバック：英語のハードコーディング
                return GetEnglishFallback(key);
            }
        }
        
        /// <summary>
        /// 英語フォールバック文字列を取得
        /// </summary>
        private static string GetEnglishFallback(string key)
        {
            switch (key)
            {
                case "TOOLTIP_RENDERIT_OPT":
                    return "Fixes performance issues in RenderIt MOD. Optimizes heavy frame processing and caches mod enablement checks.";
                case "TOOLTIP_ASPHALT_OPT":
                    return "Fixes performance issues in PloppableAsphaltFix MOD. Optimizes ApplyProperties processing that causes 838ms spikes.";
                case "TOOLTIP_GAMESETTINGS_OPT":
                    return "Reduces GameSettings save frequency from 1 second to 1 minute. Fixes 79ms/frame bottleneck caused by frequent 2.6MB userGameState.cgs file writes.";
                case "TOOLTIP_LOG_SUPPRESSION":
                    return "Suppresses PackageDeserializer warning logs. Reduces log file size and provides slight performance improvement.";
                case "TOOLTIP_START_ANALYSIS":
                    return "Runs performance and simulation analysis for 5 minutes. Generates detailed CSV reports. CPU usage will temporarily increase.";
                case "TOOLTIP_STOP_ANALYSIS":
                    return "Manually stops running analysis. Normally auto-stops after 5 minutes, but can be stopped early if needed.";
                case "TOOLTIP_COPY_MODLIST":
                    return "Copies enabled MOD list in \"WorkshopID ModName\" format to clipboard. Useful for support requests.";
                default:
                    return key; // キーをそのまま返す
            }
        }
        
        // RenderIt最適化の説明
        public static string RenderItOptimizationTooltip => GetString("TOOLTIP_RENDERIT_OPT");
        
        // PloppableAsphaltFix最適化の説明
        public static string PloppableAsphaltFixOptimizationTooltip => GetString("TOOLTIP_ASPHALT_OPT");
        
        // GameSettings最適化の説明
        public static string GameSettingsOptimizationTooltip => GetString("TOOLTIP_GAMESETTINGS_OPT");
        
        // ログ抑制の説明
        public static string LogSuppressionTooltip => GetString("TOOLTIP_LOG_SUPPRESSION");
        
        // 5分間分析の説明
        public static string FiveMinuteAnalysisTooltip => GetString("TOOLTIP_START_ANALYSIS");
        
        // 分析停止の説明
        public static string StopAnalysisTooltip => GetString("TOOLTIP_STOP_ANALYSIS");
        
        // MOD一覧コピーの説明
        public static string CopyModListTooltip => GetString("TOOLTIP_COPY_MODLIST");
    }
}
