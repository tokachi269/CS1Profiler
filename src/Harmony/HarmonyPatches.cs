using ColossalFramework;
using ColossalFramework.Plugins;
using HarmonyLib;
using ICities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using CS1Profiler.Managers;

namespace CS1Profiler
{
    public static class Patcher
    {
        private const string HarmonyId = "me.cs1profiler.startup";
        private static bool patched = false;

        public static void PatchAll()
        {
            if (patched) return;

            patched = true;
            var harmony = new HarmonyLib.Harmony(HarmonyId);

            // --- PackageDeserializerログ抑制パッチ ---
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Applying log suppression patches...");
                PatchPackageDeserializerLogs(harmony);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Log suppression patches failed: " + e.Message);
            }

            // --- 起動時解析用のパッチを追加 ---
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Applying startup analysis patches...");
                PatchStartupCriticalMethods(harmony);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Startup patches failed: " + e.Message);
            }

            // --- 既存のSimulationStepImplパッチ ---
            try
            {
                // SimulationManagerのみに限定
                var simType = typeof(SimulationManager);
                var simMethod = simType.GetMethod("SimulationStepImpl", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (simMethod != null && simMethod.GetMethodBody() != null)
                {
                    harmony.Patch(
                        original: simMethod,
                        prefix: new HarmonyLib.HarmonyMethod(typeof(Hooks).GetMethod("Pre")),
                        postfix: new HarmonyLib.HarmonyMethod(typeof(Hooks).GetMethod("Post"))
                    );
                    UnityEngine.Debug.Log("[CS1Profiler] SimulationManager.SimulationStepImpl patched successfully");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] SimulationManager patch failed: " + e.Message);
            }

            UnityEngine.Debug.Log("[CS1Profiler] Startup analysis + performance monitoring patches complete.");
        }

        private static void PatchStartupCriticalMethods(HarmonyLib.Harmony harmony)
        {
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Patching startup critical methods...");
                
                // BootStrapper.Boot のパッチ（重要な起動ボトルネック）
                PatchBootStrapper(harmony);
                
                // PackageManager.Ensure のパッチ（MOD読み込みの遅延原因）
                PatchPackageManager(harmony);
                
                // LoadingExtensionBaseの主要メソッドをパッチ
                PatchLoadingExtensions(harmony);

                UnityEngine.Debug.Log("[CS1Profiler] Startup critical methods patching completed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Startup methods patch failed: " + e.Message);
            }
        }

        // PackageDeserializerの煩雑なログを抑制するパッチ
        private static void PatchPackageDeserializerLogs(HarmonyLib.Harmony harmony)
        {
            try
            {
                var packageDeserializerType = Type.GetType("ColossalFramework.Packaging.PackageDeserializer, ColossalManaged");
                if (packageDeserializerType != null)
                {
                    // ResolveLegacyMemberメソッドを完全に置き換え
                    var resolveLegacyMemberMethod = packageDeserializerType.GetMethod("ResolveLegacyMember", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (resolveLegacyMemberMethod != null)
                    {
                        harmony.Patch(resolveLegacyMemberMethod,
                            prefix: new HarmonyLib.HarmonyMethod(typeof(LogSuppressionHooks), "ResolveLegacyMember_Prefix"));
                        UnityEngine.Debug.Log("[CS1Profiler] PackageDeserializer.ResolveLegacyMember replaced for log suppression");
                    }
                    
                    // ResolveLegacyTypeメソッドも置き換え
                    var resolveLegacyTypeMethod = packageDeserializerType.GetMethod("ResolveLegacyType", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (resolveLegacyTypeMethod != null)
                    {
                        harmony.Patch(resolveLegacyTypeMethod,
                            prefix: new HarmonyLib.HarmonyMethod(typeof(LogSuppressionHooks), "ResolveLegacyType_Replacement"));
                        UnityEngine.Debug.Log("[CS1Profiler] PackageDeserializer.ResolveLegacyType replaced for log suppression");
                    }
                    
                    // HandleUnknownTypeメソッドも置き換え
                    var handleUnknownTypeMethod = packageDeserializerType.GetMethod("HandleUnknownType", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (handleUnknownTypeMethod != null)
                    {
                        harmony.Patch(handleUnknownTypeMethod,
                            prefix: new HarmonyLib.HarmonyMethod(typeof(LogSuppressionHooks), "HandleUnknownType_Replacement"));
                        UnityEngine.Debug.Log("[CS1Profiler] PackageDeserializer.HandleUnknownType replaced for log suppression");
                    }
                }
                
                UnityEngine.Debug.Log("[CS1Profiler] PackageDeserializer log suppression replacement patches completed");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PackageDeserializer log suppression replacement patch failed: " + e.Message);
            }
        }

        private static void PatchBootStrapper(HarmonyLib.Harmony harmony)
        {
            try
            {
                var bootStrapperType = Type.GetType("ColossalFramework.BootStrapper, ColossalManaged");
                if (bootStrapperType != null)
                {
                    // Boot() メソッドをパッチ
                    var bootMethods = bootStrapperType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    foreach (var method in bootMethods)
                    {
                        if (method.Name == "Boot")
                        {
                            harmony.Patch(method,
                                prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "BootStrapper_Boot_Pre"),
                                postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "BootStrapper_Boot_Post"));
                            UnityEngine.Debug.Log("[CS1Profiler] BootStrapper.Boot patched: " + method.GetParameters().Length + " parameters");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] BootStrapper patch failed: " + e.Message);
            }
        }

        private static void PatchPackageManager(HarmonyLib.Harmony harmony)
        {
            try
            {
                var packageManagerType = Type.GetType("ColossalFramework.Packaging.PackageManager, ColossalManaged");
                if (packageManagerType != null)
                {
                    var ensureMethod = packageManagerType.GetMethod("Ensure", BindingFlags.Public | BindingFlags.Static);
                    if (ensureMethod != null)
                    {
                        harmony.Patch(ensureMethod,
                            prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "PackageManager_Ensure_Pre"),
                            postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "PackageManager_Ensure_Post"));
                        UnityEngine.Debug.Log("[CS1Profiler] PackageManager.Ensure patched successfully");
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PackageManager patch failed: " + e.Message);
            }
        }

        private static void PatchLoadingExtensions(HarmonyLib.Harmony harmony)
        {
            try
            {
                // LoadingExtensionBaseの重要なメソッドをパッチして詳細な起動解析を実行
                var extensionType = typeof(LoadingExtensionBase);
                
                var onCreatedMethod = extensionType.GetMethod("OnCreated", BindingFlags.Public | BindingFlags.Instance);
                if (onCreatedMethod != null)
                {
                    harmony.Patch(onCreatedMethod,
                        prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnCreated_Pre"),
                        postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnCreated_Post"));
                    UnityEngine.Debug.Log("[CS1Profiler] LoadingExtensionBase.OnCreated patched");
                }

                var onLevelLoadedMethod = extensionType.GetMethod("OnLevelLoaded", BindingFlags.Public | BindingFlags.Instance);
                if (onLevelLoadedMethod != null)
                {
                    harmony.Patch(onLevelLoadedMethod,
                        prefix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnLevelLoaded_Pre"),
                        postfix: new HarmonyLib.HarmonyMethod(typeof(StartupHooks), "LoadingExtension_OnLevelLoaded_Post"));
                    UnityEngine.Debug.Log("[CS1Profiler] LoadingExtensionBase.OnLevelLoaded patched");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension patch failed: " + e.Message);
            }
        }

        public static void UnpatchAll()
        {
            if (!patched) return;

            var harmony = new HarmonyLib.Harmony(HarmonyId);
            harmony.UnpatchAll(HarmonyId);
            patched = false;
        }
    }

    public static class Hooks
    {
        private static int _id;
        private static long _tsc;
        private static bool _enabled = true;
        
        private static readonly int MaxMethods = 50; // 安全な値を維持
        private static readonly int SampleEveryNFrames = 60; // 負荷軽減
        private static Dictionary<MethodBase, int> _methodToId;
        private static Dictionary<int, MethodBase> _byId;
        private static long[] _ns;
        private static int[] _cnt;
        private static int _nextId = 0;

        // 静的コンストラクタを削除し、必要時に初期化するように変更
        private static void EnsureInitialized()
        {
            try 
            {
                // 基本的なフィールドを初期化
                if (_methodToId == null) _methodToId = new Dictionary<MethodBase, int>();
                if (_byId == null) _byId = new Dictionary<int, MethodBase>();
                if (_ns == null) _ns = new long[MaxMethods];
                if (_cnt == null) _cnt = new int[MaxMethods];
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] EnsureInitialized failed: " + e.Message);
            }
        }

        public static void ToggleProfiling()
        {
            UnityEngine.Debug.Log("[CS1Profiler] === ToggleProfiling() CALLED ===");
            UnityEngine.Debug.Log("[CS1Profiler] Current _enabled state: " + _enabled);
            
            _enabled = !_enabled;
            
            UnityEngine.Debug.Log("[CS1Profiler] New _enabled state: " + _enabled);
            UnityEngine.Debug.Log("[CS1Profiler] Profiling: " + (_enabled ? "ON" : "OFF"));
            
            try
            {
                UnityEngine.Debug.Log("[CS1Profiler] Attempting CSV write...");
                // プロファイリング状態をCSVに記録（ProfilerManager経由）
                if (ProfilerManager.Instance != null && ProfilerManager.Instance.CsvManager != null)
                {
                    ProfilerManager.Instance.CsvManager.QueueCsvWrite("System", "ProfilingToggle", 0, 0, 0, 0, _enabled ? "ENABLED" : "DISABLED");
                }
                UnityEngine.Debug.Log("[CS1Profiler] CSV write completed successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] CSV write error in ToggleProfiling: " + e.Message);
            }
            
            UnityEngine.Debug.Log("[CS1Profiler] === ToggleProfiling() COMPLETED ===");
        }
        
        public static bool IsEnabled()
        {
            return _enabled;
        }

        private static int GetId(MethodBase method)
        {
            try
            {
                if (_methodToId == null || _byId == null) return 0;
                
                if (!_methodToId.TryGetValue(method, out int id))
                {
                    id = _nextId++;
                    if (id >= MaxMethods) return 0;
                    _methodToId[method] = id;
                    _byId[id] = method;
                }
                return id;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] GetId error: " + e.Message);
                return 0;
            }
        }

        private static bool ShouldSample()
        {
            if (SampleEveryNFrames <= 1) return true;
            return (UnityEngine.Time.frameCount % SampleEveryNFrames) == 0;
        }

        public static void Pre(MethodBase __originalMethod)
        {
            if (!_enabled || !ShouldSample()) return;
            EnsureInitialized();
            _id  = GetId(__originalMethod);
            _tsc = Stopwatch.GetTimestamp();
        }

        public static void Post()
        {
            if (!_enabled || !ShouldSample()) return;
            
            try
            {
                long elapsed = Stopwatch.GetTimestamp() - _tsc;
                if (_id > 0 && _id < MaxMethods && _ns != null && _cnt != null)
                {
                    _ns[_id] += elapsed * 1000000000L / Stopwatch.Frequency;
                    _cnt[_id]++;
                }
                
                // 統計出力頻度を大幅に下げる（10分間隔）
                if (UnityEngine.Time.frameCount % 36000 == 0)
                {
                    DumpTop(10);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] Post method error: " + e.Message);
            }
        }

        private static void DumpTop(int n)
        {
            try
            {
                if (_ns == null || _cnt == null || _byId == null) return;
                
                var list = new List<ProfileEntry>();
                for (int i = 1; i < _nextId && i < MaxMethods; i++)
                {
                    if (_cnt[i] > 0) list.Add(new ProfileEntry { id = i, ns = _ns[i], cnt = _cnt[i] });
                }
                
                // ラムダ式を避けるため、手動でソート
                for (int i = 0; i < list.Count - 1; i++)
                {
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        if (list[i].ns < list[j].ns)
                        {
                            var temp = list[i];
                            list[i] = list[j];
                            list[j] = temp;
                        }
                    }
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[CS1Profiler] Top" + n + " (frame " + UnityEngine.Time.frameCount + ") WITH CSV");
                
                for (int i = 0; i < Math.Min(n, list.Count); i++)
                {
                    var entry = list[i];
                    double ms = entry.ns / 1000000.0;
                    double avgUs = (ms * 1000.0) / entry.cnt;
                    var m = _byId[entry.id];
                    string typeName = m.DeclaringType != null ? m.DeclaringType.Name : "Unknown";
                    string methodName = m.Name;
                    
                    sb.Append(i+1).Append(". ")
                      .Append(typeName).Append(".").Append(methodName)
                      .Append("  total:").Append(ms.ToString("F3")).Append("ms")
                      .Append("  calls:").Append(entry.cnt)
                      .Append("  avg:").Append(avgUs.ToString("F2")).Append("us")
                      .AppendLine();
                    
                    // 各メソッドの統計をCSVキューに追加（ProfilerManager経由）
                    if (ProfilerManager.Instance != null && ProfilerManager.Instance.CsvManager != null)
                    {
                        ProfilerManager.Instance.CsvManager.QueueCsvWrite(typeName, methodName, ms, entry.cnt, avgUs, i + 1);
                    }
                    
                    _ns[entry.id] = 0; _cnt[entry.id] = 0;
                }
                
                UnityEngine.Debug.Log(sb.ToString());
                
                // 画面ログに送信
                KeyHandler.UpdateLog(sb.ToString());
                
                // システム統計もCSVに記録（ProfilerManager経由）
                if (ProfilerManager.Instance != null && ProfilerManager.Instance.CsvManager != null)
                {
                    float currentFps = 1.0f / UnityEngine.Time.deltaTime;
                    long memMB = GC.GetTotalMemory(false) / 1024 / 1024;
                    ProfilerManager.Instance.CsvManager.QueueCsvWrite("System", "FrameStats", currentFps, 1, 0, 0, "FPS=" + currentFps.ToString("F1") + ",Memory=" + memMB + "MB");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] DumpTop error: " + e.Message);
            }
        }

        private struct ProfileEntry
        {
            public int id;
            public long ns;
            public int cnt;
        }
    }

    // 起動時の重要な処理をフック・分析するクラス
    public static class StartupHooks
    {
        private static DateTime _hookStartTime;
        
        // BootStrapper.Boot() のフック
        public static void BootStrapper_Boot_Pre()
        {
            try
            {
                _hookStartTime = DateTime.Now;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] BootStrapper.Boot() - PRE");
                LogStartupEvent("BOOTSTRAPPER_BOOT_START", "BootStrapper.Boot() method started");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] BootStrapper_Boot_Pre error: " + e.Message);
            }
        }

        public static void BootStrapper_Boot_Post()
        {
            try
            {
                TimeSpan elapsed = DateTime.Now - _hookStartTime;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] BootStrapper.Boot() - POST (" + elapsed.TotalMilliseconds.ToString("F1") + "ms)");
                LogStartupEvent("BOOTSTRAPPER_BOOT_END", "BootStrapper.Boot() completed in " + elapsed.TotalMilliseconds.ToString("F1") + "ms");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] BootStrapper_Boot_Post error: " + e.Message);
            }
        }

        // PackageManager.Ensure() のフック（MOD読み込みのボトルネック）
        public static void PackageManager_Ensure_Pre()
        {
            try
            {
                _hookStartTime = DateTime.Now;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] PackageManager.Ensure() - PRE");
                LogStartupEvent("PACKAGEMANAGER_ENSURE_START", "PackageManager.Ensure() started - MOD loading phase");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PackageManager_Ensure_Pre error: " + e.Message);
            }
        }

        public static void PackageManager_Ensure_Post()
        {
            try
            {
                TimeSpan elapsed = DateTime.Now - _hookStartTime;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] PackageManager.Ensure() - POST (" + elapsed.TotalMilliseconds.ToString("F1") + "ms)");
                LogStartupEvent("PACKAGEMANAGER_ENSURE_END", "PackageManager.Ensure() completed in " + elapsed.TotalMilliseconds.ToString("F1") + "ms - MOD loading finished");
                
                // 重要：MOD読み込み完了時のメモリ使用量も記録（ProfilerManager経由）
                if (ProfilerManager.Instance != null && ProfilerManager.Instance.CsvManager != null)
                {
                    long memoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                    ProfilerManager.Instance.CsvManager.QueueCsvWrite("Memory", "PostMODLoad", memoryMB, 1, elapsed.TotalMilliseconds, 0, "Memory usage after MOD loading");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] PackageManager_Ensure_Post error: " + e.Message);
            }
        }

        // LoadingExtension.OnCreated() のフック
        public static void LoadingExtension_OnCreated_Pre(LoadingExtensionBase __instance)
        {
            try
            {
                _hookStartTime = DateTime.Now;
                string typeName = __instance.GetType().Name;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] " + typeName + ".OnCreated() - PRE");
                LogStartupEvent("LOADING_ONCREATED_START", typeName + ".OnCreated() started");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension_OnCreated_Pre error: " + e.Message);
            }
        }

        public static void LoadingExtension_OnCreated_Post(LoadingExtensionBase __instance)
        {
            try
            {
                TimeSpan elapsed = DateTime.Now - _hookStartTime;
                string typeName = __instance.GetType().Name;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] " + typeName + ".OnCreated() - POST (" + elapsed.TotalMilliseconds.ToString("F1") + "ms)");
                LogStartupEvent("LOADING_ONCREATED_END", typeName + ".OnCreated() completed in " + elapsed.TotalMilliseconds.ToString("F1") + "ms");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension_OnCreated_Post error: " + e.Message);
            }
        }

        // LoadingExtension.OnLevelLoaded() のフック
        public static void LoadingExtension_OnLevelLoaded_Pre(LoadingExtensionBase __instance, LoadMode mode)
        {
            try
            {
                _hookStartTime = DateTime.Now;
                string typeName = __instance.GetType().Name;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] " + typeName + ".OnLevelLoaded(" + mode.ToString() + ") - PRE");
                LogStartupEvent("LOADING_ONLEVELLOAD_START", typeName + ".OnLevelLoaded(" + mode.ToString() + ") started");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension_OnLevelLoaded_Pre error: " + e.Message);
            }
        }

        public static void LoadingExtension_OnLevelLoaded_Post(LoadingExtensionBase __instance, LoadMode mode)
        {
            try
            {
                TimeSpan elapsed = DateTime.Now - _hookStartTime;
                string typeName = __instance.GetType().Name;
                UnityEngine.Debug.Log("[CS1Profiler-Startup] " + typeName + ".OnLevelLoaded(" + mode.ToString() + ") - POST (" + elapsed.TotalMilliseconds.ToString("F1") + "ms)");
                LogStartupEvent("LOADING_ONLEVELLOAD_END", typeName + ".OnLevelLoaded(" + mode.ToString() + ") completed in " + elapsed.TotalMilliseconds.ToString("F1") + "ms");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] LoadingExtension_OnLevelLoaded_Post error: " + e.Message);
            }
        }

        // 共通の起動イベントログ機能（静的アクセス）
        private static void LogStartupEvent(string eventType, string description)
        {
            try
            {
                // Modクラスの静的LogStartupEventメソッドを呼び出し
                // リフレクションを使用して安全に呼び出し
                var modType = typeof(Mod);
                var method = modType.GetMethod("LogStartupEvent", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    method.Invoke(null, new object[] { eventType, description });
                }
                else
                {
                    // フォールバック：直接ログ出力
                    UnityEngine.Debug.Log("[CS1Profiler-Startup-Fallback] " + eventType + " | " + description);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] StartupHooks.LogStartupEvent error: " + e.Message);
            }
        }
    }

    // PackageDeserializerのログ抑制を行うフッククラス
    public static class LogSuppressionHooks
    {
        // ログ抑制のオン/オフ設定（デフォルトはtrue=抑制する）
        public static bool SuppressPackageDeserializerLogs = true;

        // ResolveLegacyMemberメソッドの置き換え（ログ出力部分を除去）
        public static bool ResolveLegacyMember_Prefix(Type fieldType, Type classType, string member, ref string __result)
        {
            try
            {
                if (!SuppressPackageDeserializerLogs)
                {
                    // ログ抑制が無効な場合は元のメソッドを実行
                    return true;
                }

                var packageDeserializerType = Type.GetType("ColossalFramework.Packaging.PackageDeserializer, ColossalManaged");
                if (packageDeserializerType != null)
                {
                    var handlerField = packageDeserializerType.GetField("m_ResolveLegacyMemberHandler", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (handlerField != null)
                    {
                        var handler = handlerField.GetValue(null);
                        if (handler != null)
                        {
                            // ハンドラーを呼び出して結果を取得（ログなし）
                            var delegateMethod = handler.GetType().GetMethod("Invoke");
                            var text = (string)delegateMethod.Invoke(handler, new object[] { classType, member });
                            __result = text;
                            return false; // 元のメソッドをスキップ（ログ抑制）
                        }
                    }
                }
                
                __result = member;
                return false; // 元のメソッドをスキップ
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ResolveLegacyMember_Prefix error: " + e.Message);
                return true; // エラー時は元のメソッドを実行
            }
        }

        // ResolveLegacyTypeメソッドの置き換え（ログ出力部分を除去）
        public static bool ResolveLegacyType_Replacement(string type, ref string __result)
        {
            try
            {
                var packageDeserializerType = Type.GetType("ColossalFramework.Packaging.PackageDeserializer, ColossalManaged");
                if (packageDeserializerType != null)
                {
                    var handlerField = packageDeserializerType.GetField("m_ResolveLegacyTypeHandler", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (handlerField != null)
                    {
                        var handler = handlerField.GetValue(null);
                        if (handler != null)
                        {
                            var delegateType = typeof(Func<string, string>);
                            var invokeMethod = delegateType.GetMethod("Invoke");
                            var text = (string)invokeMethod.Invoke(handler, new object[] { type });
                            
                            // ログ抑制が無効な場合のみログを出力
                            if (!SuppressPackageDeserializerLogs)
                            {
                                // ログ出力処理（省略可能）
                                UnityEngine.Debug.LogWarning("[PackageDeserializer] Unknown type detected. Attempting to resolve from '" + type + "' to '" + text + "'");
                            }
                            
                            __result = text;
                            return false;
                        }
                    }
                }
                
                __result = type;
                return false;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ResolveLegacyType_Replacement error: " + e.Message);
                return true;
            }
        }

        // HandleUnknownTypeメソッドの置き換え（ログ出力部分を除去）
        public static bool HandleUnknownType_Replacement(string type, ref int __result)
        {
            try
            {
                var packageDeserializerType = Type.GetType("ColossalFramework.Packaging.PackageDeserializer, ColossalManaged");
                if (packageDeserializerType != null)
                {
                    var handlerField = packageDeserializerType.GetField("m_UnknownTypeHandler", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (handlerField != null)
                    {
                        var handler = handlerField.GetValue(null);
                        if (handler != null)
                        {
                            var delegateType = typeof(Func<string, int>);
                            var invokeMethod = delegateType.GetMethod("Invoke");
                            var num = (int)invokeMethod.Invoke(handler, new object[] { type });
                            
                            // ログ抑制が無効な場合のみログを出力
                            if (!SuppressPackageDeserializerLogs)
                            {
                                UnityEngine.Debug.LogWarning("[PackageDeserializer] Unexpected type '" + type + "' detected. No resolver handled this type. Skipping " + num + " bytes.");
                            }
                            
                            __result = num;
                            return false;
                        }
                    }
                }
                
                __result = -1;
                return false;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] HandleUnknownType_Replacement error: " + e.Message);
                return true;
            }
        }
    }
}
