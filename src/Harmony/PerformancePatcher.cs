using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CS1Profiler.Harmony
{
    /// <summary>
    /// パフォーマンス測定用のHarmonyパッチ管理
    /// Manager/AI/Controller系クラスの実行時間を測定
    /// </summary>
    public static class PerformancePatcher
    {
        public static void ApplyBlacklistPatches(HarmonyLib.Harmony harmony, HashSet<string> modAssemblyNames, HashSet<string> modTypeNames)
        {
            try
            {
                var targetAssemblies = new List<string> { "Assembly-CSharp", "ColossalManaged" };
                targetAssemblies.AddRange(modAssemblyNames);
                
                int patchCount = 0;
                
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    string assemblyName = assembly.GetName().Name;
                    if (!targetAssemblies.Contains(assemblyName)) continue;
                    
                    try
                    {
                        // 不要なMODを除外した型をパッチ
                        foreach (var type in assembly.GetTypes().Take(100))
                        {
                            if (!IsPerformanceCriticalType(type, modTypeNames)) continue;
                            
                            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                .Where(IsPerformanceCriticalMethod)
                                .Take(20);
                            
                            foreach (var method in methods)
                            {
                                try
                                {
                                    var prefix = new HarmonyLib.HarmonyMethod(typeof(PerformanceHooks), "ProfilerPrefix");
                                    var postfix = new HarmonyLib.HarmonyMethod(typeof(PerformanceHooks), "ProfilerPostfix");
                                    
                                    harmony.Patch(method, prefix, postfix);
                                    patchCount++;
                                    
                                }
                                catch { /* 一部のメソッドはパッチできない */ }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"[CS1Profiler] Failed to process assembly {assemblyName}: {e.Message}");
                    }
                }
                
                UnityEngine.Debug.Log($"[CS1Profiler] Applied {patchCount} performance patches using blacklist system");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CS1Profiler] ApplyBlacklistPatches error: {e.Message}");
            }
        }
        
        private static bool IsPerformanceCriticalType(Type type, HashSet<string> modTypeNames)
        {
            if (type == null || type.IsAbstract || type.IsInterface || type.IsEnum) return false;
            
            // ジェネリック型安全フィルタ
            if (type.IsGenericType || 
                type.ContainsGenericParameters || 
                type.Name.Contains("`") || 
                type.Name.StartsWith("<") || 
                type.Name.Contains(">")) return false;
            
            // システムタイプ除外
            if (type.Namespace?.StartsWith("System") == true || 
                type.Namespace?.StartsWith("Microsoft") == true ||
                type.FullName?.Contains("UnityEngine") == true) return false;
            
            // 問題のあるMOD除外
            if (IsBlacklistedMod(type)) return false;
            
            // MODタイプは強制的に含める（除外リストに該当しない場合のみ）
            if (modTypeNames.Contains(type.FullName)) return true;
            
            return true; // その他はすべて対象
        }

        private static bool IsBlacklistedMod(Type type)
        {
            return type.FullName?.StartsWith("CSShared") == true ||
                   type.FullName?.StartsWith("ModTools") == true ||
                   type.FullName?.StartsWith("ExtendedAssetEditor") == true ||
                   type.FullName?.StartsWith("CS1Profiler") == true ||
                   type.FullName?.StartsWith("JsonFx") == true ||
                   type.FullName?.StartsWith("LineToolMod") == true ||
                   // Harmony関連MOD除外
                   type.FullName?.Contains("PatchAll") == true ||
                   type.FullName?.Contains("Harmony") == true ||
                   // Keybind関連MOD除外
                   type.FullName?.Contains("Keybind") == true ||
                   type.FullName?.Contains("KeyBinding") == true ||
                   type.FullName?.Contains("Shortcut") == true;
        }
        
        private static bool IsPerformanceCriticalMethod(MethodInfo method)
        {
            if (method == null || method.IsAbstract || method.IsConstructor || method.IsSpecialName) 
                return false;
            
            // 安全チェック：メソッド本体が存在するか
            var body = method.GetMethodBody();
            if (body == null) return false;
            
            // ジェネリックメソッド安全フィルタ
            if (method.IsGenericMethod || 
                method.ContainsGenericParameters ||
                method.ReturnType.IsGenericParameter ||
                method.ReturnType.ContainsGenericParameters) return false;
            
            // ローカル変数にジェネリック型が含まれるメソッドも除外
            try
            {
                foreach (var localVar in body.LocalVariables)
                {
                    if (localVar.LocalType.IsGenericParameter || 
                        localVar.LocalType.ContainsGenericParameters)
                    {
                        return false; // 除外
                    }
                }
            }
            catch
            {
                // ローカル変数情報の取得に失敗した場合は安全のため除外
                return false;
            }
            
            var methodName = method.Name.ToLower();
            
            // ブラックリスト：除外すべきメソッド
            if (IsBlacklistedMethod(methodName, method)) return false;
            
            // それ以外はすべて対象にする
            return true;
        }

        private static bool IsBlacklistedMethod(string methodName, MethodInfo method)
        {
            return methodName.StartsWith("get_") ||
                   methodName.StartsWith("set_") ||
                   methodName.StartsWith("add_") ||
                   methodName.StartsWith("remove_") ||
                   methodName.Contains("tostring") ||
                   methodName.Contains("gethashcode") ||
                   methodName.Contains("equals") ||
                   methodName.Contains("finalize") ||
                   methodName.Contains("dispose") ||
                   methodName.Contains("destroy") ||
                   methodName.Contains("unload") ||
                   methodName.Contains("delete") ||
                   methodName.Contains("abort") ||
                   methodName.Contains("exit") ||
                   methodName.Contains("quit") ||
                   methodName.StartsWith("__") ||
                   // Harmonyメソッド除外
                   methodName.Equals("patchall") ||
                   methodName.Equals("unpatchall") ||
                   methodName.Equals("prefixmethod") ||
                   methodName.Equals("postfixmethod") ||
                   methodName.Equals("transpilemethod") ||
                   methodName.Equals("unpatchmethod") ||
                   methodName.Contains("patch") && (methodName.Contains("prefix") || methodName.Contains("postfix") || methodName.Contains("transpile")) ||
                   method.GetParameters().Length > 10; // パラメータが多すぎるメソッドは除外
        }
    }

    /// <summary>
    /// パフォーマンス測定用のHookクラス
    /// PatchControllerによる統一された有効/無効制御を使用
    /// </summary>
    public static class PerformanceHooks
    {
        // Prefix: メソッド開始時にタイムスタンプを記録
        public static void ProfilerPrefix(MethodBase __originalMethod, out long __state)
        {
            try
            {
                // 統一制御による有効/無効チェック
                if (!PatchController.PerformanceProfilingEnabled)
                {
                    __state = 0;
                    return;
                }

                string methodKey = __originalMethod.DeclaringType.Name + "." + __originalMethod.Name;
                __state = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ProfilerPrefix error: " + e.Message);
                __state = 0;
            }
        }

        // Postfix: メソッド終了時に実行時間を計算してMethodProfilerに送信
        public static void ProfilerPostfix(MethodBase __originalMethod, long __state)
        {
            try
            {
                // 統一制御による有効/無効チェック
                if (!PatchController.PerformanceProfilingEnabled || __state == 0) return;
                
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - __state;
                string methodKey = __originalMethod.DeclaringType.Name + "." + __originalMethod.Name;
                
                // 既存のPerformanceProfilerシステムを活用
                CS1Profiler.Profiling.PerformanceProfiler.RecordMethodExecution(methodKey, elapsed);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[CS1Profiler] ProfilerPostfix error: " + e.Message);
            }
        }
    }
}
