using ICities;
using System;
using UnityEngine;
using CS1Profiler.Core;
using CS1Profiler.Managers;
using CS1Profiler.Harmony;
using ColossalFramework.UI;
using CS1Profiler.TranslationFramework;

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
                        
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Complete analysis started (Performance + Simulation, 5-minute timer)");
                        UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Waiting 10 seconds before starting CSV output (patch stabilization)");
                        
                        // 10秒後にCSV出力を開始するコルーチンを開始
                        if (ProfilerManager.Instance != null)
                        {
                            ProfilerManager.Instance.StartCoroutine(modInstance.StartCSVOutputAfterDelay());
                        }
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
                }, "tooltip.enable_simulation_timer");
                
                CreateButtonWithTooltip(analysisGroup, "⏹️ Stop Analysis", () => {
                    // Performance Profiling停止
                    if (Mod.PerformanceProfilingTimerActive)
                    {
                        PatchController.PerformanceProfilingEnabled = false;
                        Mod.PerformanceProfilingTimerActive = false;
                        Mod.PerformanceProfilingStartTime = DateTime.MinValue;
                    }
                    
                    // Simulation Profiling停止
                    if (Mod.SimulationProfilingTimerActive)
                    {
                        PatchController.SimulationProfilingEnabled = false;
                        Mod.SimulationProfilingTimerActive = false;
                        Mod.SimulationProfilingStartTime = DateTime.MinValue;
                    }
                    
                    UnityEngine.Debug.Log($"{Constants.LOG_PREFIX} Complete analysis stopped manually (Performance + Simulation)");
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
    }
}
