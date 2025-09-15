using ICities;
using System;
using System.Linq;
using UnityEngine;
using CS1Profiler.Core;
using CS1Profiler.Managers;
using CS1Profiler.Harmony;
using ColossalFramework.UI;
using CS1Profiler.TranslationFramework;
using ColossalFramework;
using ColossalFramework.Plugins;

namespace CS1Profiler.UI
{
    /// <summary>
    /// MOD設定UI管理クラス
    /// UserMod.OnSettingsUIから分離したUI構築ロジック
    /// </summary>
    public static class SettingsUI
    {
        /// <summary>
        /// UIコンポーネントにツールチップを追加（翻訳ID対応）
        /// </summary>
        /// <param name="component">UIコンポーネント</param>
        /// <param name="tooltipId">翻訳ID</param>
        private static void AddTooltip(UIComponent component, string tooltipId)
        {
            try
            {
                if (component != null && !string.IsNullOrEmpty(tooltipId))
                {
                    string translatedTooltip = Translations.Translate(tooltipId);
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Tooltip translation: '{tooltipId}' -> '{translatedTooltip}'");
                    if (!string.IsNullOrEmpty(translatedTooltip))
                    {
                        component.tooltip = translatedTooltip;
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Empty translation for: {tooltipId}");
                        component.tooltip = tooltipId; // フォールバック
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Failed to add tooltip: {e.Message}");
                // フォールバック
                if (component != null)
                {
                    component.tooltip = tooltipId;
                }
            }
        }
        
        /// <summary>
        /// チェックボックスを作成してツールチップを追加（翻訳ID対応）
        /// </summary>
        private static UICheckBox CreateCheckboxWithTooltip(UIHelperBase group, string text, bool defaultValue, OnCheckChanged eventCallback, string tooltipId)
        {
            var checkbox = (UICheckBox)group.AddCheckbox(text, defaultValue, eventCallback);
            AddTooltip(checkbox, tooltipId);
            return checkbox;
        }
        
        /// <summary>
        /// ボタンを作成してツールチップを追加（翻訳ID対応）
        /// </summary>
        private static UIButton CreateButtonWithTooltip(UIHelperBase group, string text, OnButtonClicked eventCallback, string tooltipId)
        {
            var button = (UIButton)group.AddButton(text, eventCallback);
            AddTooltip(button, tooltipId);
            return button;
        }
        /// <summary>
        /// メイン設定UI構築メソッド
        /// </summary>
        /// <param name="helper">Cities: Skylines UI Helper</param>
        /// <param name="modInstance">UserModインスタンス（必要に応じてコールバック用）</param>
        public static void BuildSettingsUI(UIHelperBase helper, Mod modInstance)
        {
            try
            {
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} OnSettingsUI starting...");
                
                // グループ1: MOD最適化（ゲームプレイヤーが常に使うもの）
                var optimizationGroup = helper.AddGroup("MOD Optimizations");
                
                CreateCheckboxWithTooltip(optimizationGroup, "RenderIt Optimization:", 
                    PatchController.RenderItOptimizationEnabled, 
                    (value) => {
                        PatchController.RenderItOptimizationEnabled = value;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} RenderIt optimization: " + (value ? "ENABLED" : "DISABLED"));
                    },
                    "tooltip.enable_renderit_optimization");

                CreateCheckboxWithTooltip(optimizationGroup, "PloppableAsphaltFix Optimization:", 
                    PatchController.PloppableAsphaltFixOptimizationEnabled, 
                    (value) => {
                        PatchController.PloppableAsphaltFixOptimizationEnabled = value;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PloppableAsphaltFix optimization: " + (value ? "ENABLED" : "DISABLED"));
                    },
                    "tooltip.enable_renderit_optimization");

                CreateCheckboxWithTooltip(optimizationGroup, "RenderManager Optimization:", 
                    true, // デフォルトON
                    (value) => {
                        PatchController.FpsBoosterAnalysisEnabled = value;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} RenderManager Optimization: " + (value ? "ENABLED" : "DISABLED"));
                    },
                    "tooltip.enable_rendermanager_analysis");

                CreateCheckboxWithTooltip(optimizationGroup, "GameSettings Save Optimization:", 
                    PatchController.GameSettingsOptimizationEnabled, 
                    (value) => {
                        PatchController.GameSettingsOptimizationEnabled = value;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} GameSettings save optimization: " + (value ? "ENABLED" : "DISABLED"));
                    },
                    "TOOLTIP_GAMESETTINGS_OPT");

                // グループ2: 性能分析とシステム情報
                var analysisGroup = helper.AddGroup("Performance Analysis & System");
                CreateCheckboxWithTooltip(analysisGroup, "Suppress PackageDeserializer Warnings:", 
                    LogSuppressionHooks.SuppressPackageDeserializerLogs, 
                    (value) => {
                        LogSuppressionHooks.SuppressPackageDeserializerLogs = value;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} PackageDeserializer log suppression: " + (value ? "ENABLED" : "DISABLED"));
                    },
                    "tooltip.enable_harmony_patches");
                // ステータス情報
                string profilingStatus = "STOPPED";
                string csvPath = "Not available";
                
                try
                {
                    if (ProfilerManager.Instance != null)
                    {
                        profilingStatus = ProfilerManager.Instance.IsProfilingEnabled() ? "ACTIVE" : "STOPPED";
                        csvPath = ProfilerManager.Instance.GetCsvPath();
                        if (!string.IsNullOrEmpty(csvPath) && csvPath.Length > 50)
                        {
                            csvPath = "..." + csvPath.Substring(csvPath.Length - 47);
                        }
                    }
                }
                catch
                {
                    profilingStatus = "ERROR";
                    csvPath = "Unable to get path";
                }
                
                analysisGroup.AddTextfield("Profiling Status:", profilingStatus, null);
                analysisGroup.AddTextfield("CSV Output:", csvPath, null);
                
                // Performance & Simulation Profiling - 5分間の一時計測ボタン（統合版）
                bool anyProfilingActive = Mod.PerformanceProfilingTimerActive || Mod.SimulationProfilingTimerActive;
                
                // 分析制御ボタン（常に表示）
                CreateButtonWithTooltip(analysisGroup, "Start 5-min Analysis", () => {
                    ShowPerformanceAnalysisWarning();
                }, "tooltip.enable_simulation_timer");
                
                CreateButtonWithTooltip(analysisGroup, "Stop Analysis", () => {
                    // 1. 即座にCSV自動出力を停止
                    Mod.SetCsvAutoOutputEnabled(false);
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} CSV auto-output DISABLED immediately");
                    
                    // 2. 最後のCSV出力を実行
                    var profilerManager = ProfilerManager.Instance;
                    if (profilerManager != null)
                    {
                        try
                        {
                            profilerManager.ExportToCSV();
                            UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Final CSV export completed before stop");
                        }
                        catch (Exception e)
                        {
                            UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Error during final CSV export: {e.Message}");
                        }
                    }
                    
                    // 3. Performance Profiling停止
                    if (Mod.PerformanceProfilingTimerActive)
                    {
                        PatchController.PerformanceProfilingEnabled = false;
                        Mod.PerformanceProfilingTimerActive = false;
                        Mod.PerformanceProfilingStartTime = DateTime.MinValue;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance profiling stopped");
                    }
                    
                    // 4. Simulation Profiling停止
                    if (Mod.SimulationProfilingTimerActive)
                    {
                        PatchController.SimulationProfilingEnabled = false;
                        Mod.SimulationProfilingTimerActive = false;
                        Mod.SimulationProfilingStartTime = DateTime.MinValue;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Simulation profiling stopped");
                    }
                    
                    // 5. パッチを無効化（時間がかかる処理なので最後に実行）
                    try
                    {
                        PatchController.DisablePatches();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} All patches disabled");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Error disabling patches: {e.Message}");
                    }
                    
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Complete analysis stopped manually (CSV output disabled, Performance + Simulation stopped, Patches disabled)");
                }, "tooltip.enable_rendering_timer");
                
                // System Information
                analysisGroup.AddTextfield("Current Status:", modInstance.GetPatchStatus(), null);
                
                // 設定データエクスポート機能
                analysisGroup.AddSpace(10);
                var exportGroup = helper.AddGroup("Settings Data Export");
                
                CreateButtonWithTooltip(exportGroup, "📋 Export userGameState to Clipboard", () => {
                    try
                    {
                        CS1Profiler.Profiling.SettingsExporter.ExportUserGameStateToClipboard();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} userGameState data exported to clipboard");
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to export userGameState: " + ex.Message);
                    }
                }, "tooltip.export_usergamestate");
                
                CreateButtonWithTooltip(exportGroup, "📋 Export All Settings Summary to Clipboard", () => {
                    try
                    {
                        CS1Profiler.Profiling.SettingsExporter.ExportAllSettingsSummaryToClipboard();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Settings summary exported to clipboard");
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to export settings summary: " + ex.Message);
                    }
                }, "tooltip.export_all_settings");
                
                // 【一時的】Building分析用のテストボタン（後で削除予定）
                var buildingAnalysisGroup = helper.AddGroup("Building Analysis (Temporary)");
                
                CreateButtonWithTooltip(buildingAnalysisGroup, "🏢 Enable Building Analysis Patch", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Enabling Building RenderInstance analysis patch...");
                        CS1Profiler.Harmony.PatchController.BuildingRenderAnalysisEnabled = true;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building analysis patch enabled successfully");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable building analysis patch: {e.Message}");
                    }
                }, "Enable building analysis patch");
                
                CreateButtonWithTooltip(buildingAnalysisGroup, "▶️ Start Building Analysis", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Starting Building RenderInstance analysis...");
                        CS1Profiler.Harmony.BuildingRenderAnalysisHooks.StartAnalysis();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building analysis started successfully");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to start building analysis: {e.Message}");
                    }
                }, "Start building render analysis");
                
                CreateButtonWithTooltip(buildingAnalysisGroup, "📊 Show Analysis Results", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Printing Building RenderInstance analysis results...");
                        CS1Profiler.Harmony.BuildingRenderAnalysisHooks.PrintResults();
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to print building analysis results: {e.Message}");
                    }
                }, "Show building analysis results");
                
                CreateButtonWithTooltip(buildingAnalysisGroup, "⏹️ Stop Building Analysis", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Stopping Building RenderInstance analysis...");
                        CS1Profiler.Harmony.BuildingRenderAnalysisHooks.StopAnalysis();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building analysis stopped successfully");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to stop building analysis: {e.Message}");
                    }
                }, "Stop building analysis");
                
                CreateButtonWithTooltip(buildingAnalysisGroup, "🔄 Reset Counters", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Resetting Building RenderInstance analysis counters...");
                        CS1Profiler.Harmony.BuildingRenderAnalysisHooks.ResetCounters();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building analysis counters reset successfully");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to reset building analysis counters: {e.Message}");
                    }
                }, "Reset building analysis counters");
                
                CreateButtonWithTooltip(buildingAnalysisGroup, "🏢 Disable Building Analysis Patch", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Disabling Building RenderInstance analysis patch...");
                        CS1Profiler.Harmony.PatchController.BuildingRenderAnalysisEnabled = false;
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Building analysis patch disabled successfully");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable building analysis patch: {e.Message}");
                    }
                }, "Disable building analysis patch");

                // Propバッチング検証用のテストボタン
                var propBatchingGroup = helper.AddGroup("Prop Batching Test (Performance)");
                
                CreateButtonWithTooltip(propBatchingGroup, "📦 Enable LOD Batching", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Enabling prop LOD batching...");
                        PropBatchingTestManager.SetPropBatching(true);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Prop LOD batching enabled successfully");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable prop batching: {e.Message}");
                    }
                }, "Enable prop LOD batching for performance testing");
                
                CreateButtonWithTooltip(propBatchingGroup, "🔍 Enable High-Detail Batching", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Enabling prop high-detail batching...");
                        PropBatchingTestManager.SetPropBatching(true, true);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Prop high-detail batching enabled successfully");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable high-detail prop batching: {e.Message}");
                    }
                }, "Enable high-detail prop batching (experimental)");
                
                CreateButtonWithTooltip(propBatchingGroup, "🚫 Disable All Batching", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Disabling all prop batching...");
                        PropBatchingTestManager.SetPropBatching(false);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Prop batching disabled successfully");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable prop batching: {e.Message}");
                    }
                }, "Disable all prop batching");
                
                CreateButtonWithTooltip(propBatchingGroup, "🔄 Restore Original State", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Restoring original prop batching state...");
                        PropBatchingTestManager.RestoreOriginalBatching();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Original prop batching state restored successfully");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to restore prop batching state: {e.Message}");
                    }
                }, "Restore original prop batching configuration");
                
                CreateButtonWithTooltip(propBatchingGroup, "📊 Show Batching Status", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Showing prop batching status...");
                        string status = $"Batching enabled: {PropBatchingTestManager.IsBatchingEnabled}, " +
                                      $"Override active: {PropBatchingTestManager.IsOverrideActive}, " +
                                      $"High-detail enabled: {PropBatchingTestManager.IsHighDetailBatchingEnabled}";
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Prop batching status: {status}");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to show prop batching status: {e.Message}");
                    }
                }, "Show current prop batching status");
                
                CreateButtonWithTooltip(propBatchingGroup, "🔍 Analyze Batching Details", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Analyzing detailed prop batching state...");
                        PropBatchingTestManager.AnalyzeBatchingState();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Batching analysis completed");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to analyze batching state: {e.Message}");
                    }
                }, "Analyze detailed prop batching statistics");
                
                CreateButtonWithTooltip(propBatchingGroup, "🚀 Start Performance Benchmark", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Starting performance benchmark...");
                        PropBatchingTestManager.StartPerformanceBenchmark();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance benchmark started");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to start performance benchmark: {e.Message}");
                    }
                }, "Start frame-by-frame performance measurement");
                
                CreateButtonWithTooltip(propBatchingGroup, "⏹️ Stop Performance Benchmark", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Stopping performance benchmark...");
                        PropBatchingTestManager.StopPerformanceBenchmark();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance benchmark stopped");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to stop performance benchmark: {e.Message}");
                    }
                }, "Stop performance measurement and show results");
                
                CreateButtonWithTooltip(propBatchingGroup, "⚡ Enable Performance Optimization", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Enabling performance optimization...");
                        PropBatchingTestManager.OptimizeRenderDistances(true);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance optimization enabled");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable performance optimization: {e.Message}");
                    }
                }, "Enable aggressive performance optimization (LOD distance reduction + batching)");
                
                CreateButtonWithTooltip(propBatchingGroup, "🔧 Disable Performance Optimization", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Disabling performance optimization...");
                        PropBatchingTestManager.OptimizeRenderDistances(false);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Performance optimization disabled");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to disable performance optimization: {e.Message}");
                    }
                }, "Disable performance optimization and restore original settings");
                
                CreateButtonWithTooltip(propBatchingGroup, "💥 Enable Aggressive Optimization", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Enabling aggressive optimization...");
                        PropBatchingTestManager.OptimizeAggressiveCulling(true);
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Aggressive optimization enabled - WARNING: Visual quality reduced!");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to enable aggressive optimization: {e.Message}");
                    }
                }, "Enable maximum performance optimization (major visual quality reduction)");

                // フレーム詳細記録機能
                CreateButtonWithTooltip(buildingAnalysisGroup, "🎬 Start Frame Recording", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Starting frame detail recording...");
                        CS1Profiler.Harmony.BuildingRenderAnalysisHooks.StartFrameDetailRecording();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Frame detail recording started - recording next 10 frames");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to start frame recording: {e.Message}");
                    }
                }, "Start recording detailed frame-by-frame data");

                CreateButtonWithTooltip(buildingAnalysisGroup, "⏹️ Stop Frame Recording", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Stopping frame detail recording...");
                        CS1Profiler.Harmony.BuildingRenderAnalysisHooks.StopFrameDetailRecording();
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Frame detail recording stopped");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to stop frame recording: {e.Message}");
                    }
                }, "Stop frame detail recording");

                CreateButtonWithTooltip(buildingAnalysisGroup, "📋 Show Frame Details", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Showing frame detail analysis...");
                        CS1Profiler.Harmony.BuildingRenderAnalysisHooks.PrintFrameDetails();
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to show frame details: {e.Message}");
                    }
                }, "Show detailed frame-by-frame analysis");
                
                // Building.RenderInstance詳細分析グループ
                var renderInstanceDetailGroup = helper.AddGroup("Building.RenderInstance Detail Analysis");
                
                CreateButtonWithTooltip(renderInstanceDetailGroup, "🔍 Start Detail Analysis", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Starting Building.RenderInstance detail analysis...");
                        CS1Profiler.Harmony.PatchController.BuildingRenderInstanceDetailEnabled = true;
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to start detail analysis: {e.Message}");
                    }
                }, "Start detailed analysis of Building.RenderInstance stages");

                CreateButtonWithTooltip(renderInstanceDetailGroup, "📊 Show Detail Stats", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Showing Building.RenderInstance detail stats...");
                        CS1Profiler.Harmony.BuildingRenderInstanceDetailHooks.OutputDetailedStats();
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to show detail stats: {e.Message}");
                    }
                }, "Show detailed statistics for each RenderInstance stage");

                CreateButtonWithTooltip(renderInstanceDetailGroup, "🛑 Stop Detail Analysis", () => {
                    try
                    {
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Stopping Building.RenderInstance detail analysis...");
                        CS1Profiler.Harmony.PatchController.BuildingRenderInstanceDetailEnabled = false;
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to stop detail analysis: {e.Message}");
                    }
                }, "Stop detailed analysis of Building.RenderInstance");
                
                // システム情報
                analysisGroup.AddSpace(10);
                analysisGroup.AddTextfield("MOD Version:", "1.0.0", null);
                analysisGroup.AddTextfield("Framework:", System.Environment.Version.ToString(), null);
                
                // MOD一覧機能
                analysisGroup.AddSpace(5);
                CreateButtonWithTooltip(analysisGroup, "📋 Copy MOD List to Clipboard", () => {
                    Mod.CopyModListToClipboard();
                }, "tooltip.export_logs");
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} OnSettingsUI completed successfully");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} OnSettingsUI failed: " + e.ToString());
                
                // フォールバック
                try
                {
                    var fallbackGroup = helper.AddGroup("CS1 Profiler [Error]");
                    fallbackGroup.AddTextfield("Status:", "Configuration error occurred", null);
                    fallbackGroup.AddTextfield("Solution:", "Check game logs for details", null);
                }
                catch
                {
                    UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Complete UI failure");
                }
            }

        }
        
        /// <summary>
        /// エラー時のフォールバックUI
        /// </summary>
        private static void BuildFallbackUI(UIHelperBase helper)
        {
            try
            {
                var fallbackGroup = helper.AddGroup("CS1 Profiler [Error]");
                fallbackGroup.AddTextfield("Status:", "Configuration error occurred", null);
                fallbackGroup.AddTextfield("Solution:", "Check game logs for details", null);
            }
            catch
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Complete UI failure");
            }
        }
        
        /// <summary>
        /// パフォーマンス分析開始前の警告ポップアップを表示
        /// </summary>
        private static void ShowPerformanceAnalysisWarning()
        {
            try
            {
                var messageBox = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
                if (messageBox != null)
                {
                    // 多言語対応
                    string title = Translations.Translate("warning.performance_analysis.title");
                    string message = Translations.Translate("warning.performance_analysis.message");
                    string cancelText = Translations.Translate("warning.performance_analysis.button_cancel");
                    string continueText = Translations.Translate("warning.performance_analysis.button_continue");
                    
                    messageBox.SetMessage(
                        !string.IsNullOrEmpty(title) ? title : "Performance Analysis Warning",
                        !string.IsNullOrEmpty(message) ? message : 
                        "⚠️ This function executes heavy processing. Save your game before starting. Continue?",
                        false
                    );
                    
                    // ExceptionPanelの実際のボタン構造に合わせて修正
                    var okButton = messageBox.Find<UIButton>("Ok");
                    var copyButton = messageBox.Find<UIButton>("Copy");
                    
                    if (okButton != null)
                    {
                        okButton.text = !string.IsNullOrEmpty(continueText) ? continueText : "Continue";
                        // 既存のイベントをクリアしてから新しいイベントを追加
                        okButton.eventClick += (component, eventParam) => {
                            messageBox.component.isVisible = false;
                            StartPerformanceAnalysis();
                        };
                    }
                    
                    if (copyButton != null)
                    {
                        copyButton.text = !string.IsNullOrEmpty(cancelText) ? cancelText : "Cancel";
                        // 既存のイベントをクリアしてから新しいイベントを追加
                        copyButton.eventClick += (component, eventParam) => {
                            messageBox.component.isVisible = false;
                        };
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to show warning dialog: {e.Message}");
                // ポップアップ表示に失敗した場合は直接開始
                StartPerformanceAnalysis();
            }
        }
        
        /// <summary>
        /// 実際にパフォーマンス分析を開始
        /// </summary>
        private static void StartPerformanceAnalysis()
        {
            try
            {
                // Performance Profiling開始（パッチ適用）
                PatchController.PerformanceProfilingEnabled = true;
                Mod.PerformanceProfilingStartTime = DateTime.Now;
                Mod.PerformanceProfilingTimerActive = true;
                
                // Simulation Profiling開始（パッチ適用）
                PatchController.SimulationProfilingEnabled = true;
                Mod.SimulationProfilingStartTime = DateTime.Now;
                Mod.SimulationProfilingTimerActive = true;
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} ⚠️ HEAVY ANALYSIS STARTED - Performance + Simulation (5-minute timer)");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Please avoid heavy operations. Analysis will auto-stop after 5 minutes.");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Waiting 10 seconds before starting CSV output (patch stabilization)");
                
                // 10秒後にCSV出力を開始するコルーチンを開始
                if (ProfilerManager.Instance != null)
                {
                    try
                    {
                        var plugins = PluginManager.instance.GetPluginsInfo();
                        var cs1ProfilerPlugin = plugins.Where(p => p.name.Contains("CS1Profiler")).FirstOrDefault();
                        var userMod = cs1ProfilerPlugin?.userModInstance as Mod;
                        if (userMod != null)
                        {
                            ProfilerManager.Instance.StartCoroutine(userMod.StartCSVOutputAfterDelay());
                        }
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"{Constants.LOG_PREFIX} Could not start CSV delay coroutine: {e.Message}");
                    }
                }
                
                // セーブ推奨メッセージを表示
                ShowSaveRecommendationToast();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{Constants.LOG_PREFIX} Failed to start analysis: " + ex.Message);
                // エラー時はタイマーをリセット
                Mod.PerformanceProfilingTimerActive = false;
                Mod.SimulationProfilingTimerActive = false;
                Mod.PerformanceProfilingStartTime = DateTime.MinValue;
                Mod.SimulationProfilingStartTime = DateTime.MinValue;
            }
        }
        
        /// <summary>
        /// セーブ推奨のトーストメッセージを表示
        /// </summary>
        private static void ShowSaveRecommendationToast()
        {
            try
            {
                // 多言語対応のログメッセージ
                string saveRecommendation = Translations.Translate("warning.performance_analysis.save_recommendation");
                string timerInfo = Translations.Translate("warning.performance_analysis.timer_info");
                
                string saveMsg = !string.IsNullOrEmpty(saveRecommendation) ? saveRecommendation : 
                    "💾 RECOMMENDATION: Please save your game now for safety!";
                string timerMsg = !string.IsNullOrEmpty(timerInfo) ? timerInfo : 
                    "⏱️ Analysis will run for 5 minutes and auto-stop";
                
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {saveMsg}");
                UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} {timerMsg}");
            }
            catch
            {
                // 何もしない（安全のため）
            }
        }
    }
}
