using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ColossalFramework;
using System.Reflection;

namespace CS1Profiler.Profiling
{
    /// <summary>
    /// ゲーム設定データをJSON形式で出力・クリップボードコピー
    /// </summary>
    public static class SettingsExporter
    {
        /// <summary>
        /// userGameState設定ファイルの内容をJSON形式でクリップボードにコピー
        /// </summary>
        public static void ExportUserGameStateToClipboard()
        {
            try
            {
                var settingsFile = GameSettings.FindSettingsFileByName("userGameState");
                if (settingsFile == null)
                {
                    Debug.LogWarning($"{CS1Profiler.Core.Constants.LOG_PREFIX} userGameState settings file not found");
                    
                    // フォールバック: 利用可能な設定ファイル一覧を出力
                    ExportAllSettingsSummaryToClipboard();
                    return;
                }

                Debug.Log($"{CS1Profiler.Core.Constants.LOG_PREFIX} Starting userGameState export...");
                var jsonData = ExportSettingsFileToJson(settingsFile);
                
                if (jsonData.Length > 1000000) // 1MB以上の場合警告
                {
                    Debug.LogWarning($"{CS1Profiler.Core.Constants.LOG_PREFIX} Large data size: {jsonData.Length / 1024}KB. This may take time to copy to clipboard.");
                }
                
                GUIUtility.systemCopyBuffer = jsonData;
                
                Debug.Log($"{CS1Profiler.Core.Constants.LOG_PREFIX} userGameState data exported to clipboard ({jsonData.Length / 1024}KB)");
            }
            catch (Exception e)
            {
                Debug.LogError($"{CS1Profiler.Core.Constants.LOG_PREFIX} Failed to export userGameState: {e.Message}");
                Debug.LogError($"{CS1Profiler.Core.Constants.LOG_PREFIX} Stack trace: {e.StackTrace}");
            }
        }

        /// <summary>
        /// 全ての設定ファイルの概要をJSON形式でクリップボードにコピー
        /// </summary>
        public static void ExportAllSettingsSummaryToClipboard()
        {
            try
            {
                var summary = new Dictionary<string, object>();
                
                // GameSettingsインスタンスから設定ファイル一覧を取得
                var gameSettings = Singleton<GameSettings>.instance;
                var settingsFilesField = typeof(GameSettings).GetField("m_SettingsFiles", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (settingsFilesField != null)
                {
                    var settingsFiles = settingsFilesField.GetValue(gameSettings) as System.Collections.IDictionary;
                    if (settingsFiles != null)
                    {
                        foreach (System.Collections.DictionaryEntry kvp in settingsFiles)
                        {
                            string fileName = kvp.Key?.ToString();
                            var settingsFile = kvp.Value as SettingsFile;
                            
                            if (settingsFile != null)
                            {
                                var fileInfo = new Dictionary<string, object>
                                {
                                    ["fileName"] = fileName,
                                    ["pathName"] = settingsFile.pathName,
                                    ["isDirty"] = settingsFile.isDirty,
                                    ["isSystem"] = settingsFile.isSystem,
                                    ["version"] = settingsFile.version
                                };

                                // ファイルサイズを取得
                                try
                                {
                                    if (System.IO.File.Exists(settingsFile.pathName))
                                    {
                                        var fileSize = new System.IO.FileInfo(settingsFile.pathName).Length;
                                        fileInfo["fileSizeBytes"] = fileSize;
                                        fileInfo["fileSizeKB"] = Math.Round(fileSize / 1024.0, 2);
                                    }
                                }
                                catch { }

                                summary[fileName] = fileInfo;
                            }
                        }
                    }
                }

                var jsonData = ConvertToJson(summary);
                GUIUtility.systemCopyBuffer = jsonData;
                
                Debug.Log($"{CS1Profiler.Core.Constants.LOG_PREFIX} Settings summary exported to clipboard ({jsonData.Length} characters)");
            }
            catch (Exception e)
            {
                Debug.LogError($"{CS1Profiler.Core.Constants.LOG_PREFIX} Failed to export settings summary: {e.Message}");
            }
        }

        /// <summary>
        /// 指定された設定ファイルをJSON形式に変換
        /// </summary>
        private static string ExportSettingsFileToJson(SettingsFile settingsFile)
        {
            var data = new Dictionary<string, object>();

            // 基本情報
            data["fileName"] = settingsFile.fileName;
            data["pathName"] = settingsFile.pathName;
            data["version"] = settingsFile.version;
            data["isDirty"] = settingsFile.isDirty;
            data["isSystem"] = settingsFile.isSystem;
            data["exportTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // ファイルサイズ
            try
            {
                if (System.IO.File.Exists(settingsFile.pathName))
                {
                    var fileSize = new System.IO.FileInfo(settingsFile.pathName).Length;
                    data["fileSizeBytes"] = fileSize;
                    data["fileSizeKB"] = Math.Round(fileSize / 1024.0, 2);
                }
            }
            catch { }

            // 設定値を抽出 (リフレクションを使用)
            var settingsData = new Dictionary<string, object>();
            
            try
            {
                // SettingsFileの内部フィールドにアクセス
                var type = typeof(SettingsFile);
                var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    var fieldName = field.Name;
                    if (fieldName.StartsWith("m_Settings") && field.FieldType.IsGenericType)
                    {
                        var fieldValue = field.GetValue(settingsFile);
                        if (fieldValue != null)
                        {
                            var dict = fieldValue as System.Collections.IDictionary;
                            if (dict != null)
                            {
                                var typeData = new Dictionary<string, object>();
                                var count = 0;
                                var sampleEntries = new Dictionary<string, object>();

                                foreach (System.Collections.DictionaryEntry kvp in dict)
                                {
                                    count++;
                                    if (count <= 20) // 最初の20個のみサンプルとして記録
                                    {
                                        try
                                        {
                                            sampleEntries[kvp.Key?.ToString() ?? "null"] = kvp.Value?.ToString() ?? "null";
                                        }
                                        catch
                                        {
                                            sampleEntries[kvp.Key?.ToString() ?? "null"] = "[error reading value]";
                                        }
                                    }
                                }

                                typeData["count"] = count;
                                typeData["sampleEntries"] = sampleEntries;
                                if (count > 20)
                                {
                                    typeData["note"] = $"Showing first 20 entries out of {count} total";
                                }

                                settingsData[fieldName] = typeData;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                settingsData["error"] = $"Failed to extract settings data: {e.Message}";
            }

            data["settingsData"] = settingsData;

            return ConvertToJson(data);
        }

        /// <summary>
        /// オブジェクトを簡易JSON形式に変換
        /// </summary>
        private static string ConvertToJson(object obj, int indent = 0)
        {
            var sb = new StringBuilder();
            var indentStr = new string(' ', indent * 2);

            if (obj == null)
            {
                sb.Append("null");
            }
            else if (obj is string str)
            {
                sb.Append($"\"{EscapeJsonString(str)}\"");
            }
            else if (obj is bool b)
            {
                sb.Append(b ? "true" : "false");
            }
            else if (obj is int || obj is long || obj is float || obj is double)
            {
                sb.Append(obj.ToString());
            }
            else if (obj is IDictionary<string, object> dict)
            {
                sb.AppendLine("{");
                var keys = new List<string>();
                foreach (var key in dict.Keys)
                {
                    keys.Add(key);
                }
                
                for (int i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    var value = dict[key];
                    sb.Append($"{indentStr}  \"{EscapeJsonString(key)}\": ");
                    sb.Append(ConvertToJson(value, indent + 1));
                    if (i < keys.Count - 1)
                    {
                        sb.Append(",");
                    }
                    sb.AppendLine();
                }
                sb.Append($"{indentStr}}}");
            }
            else
            {
                sb.Append($"\"{EscapeJsonString(obj.ToString())}\"");
            }

            return sb.ToString();
        }

        /// <summary>
        /// JSON文字列のエスケープ
        /// </summary>
        private static string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return str.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     .Replace("\n", "\\n")
                     .Replace("\r", "\\r")
                     .Replace("\t", "\\t");
        }
    }
}
